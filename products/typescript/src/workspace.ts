import fs from "node:fs";
import path from "node:path";
import ts from "typescript";
import { FileSystemCache } from "./cache.js";

export type SymbolLocation = { file: string; line: number; column: number };

export type SymbolInfo = {
  name: string;
  qualifiedName: string;
  kind: string;
  signature: string;
  documentation?: string;
  namespace: string;
  containingType?: string;
  location: SymbolLocation;
  declaration: ts.Declaration;
};

export type ReferenceInfo = {
  file: string;
  line: number;
  column: number;
  kind: string;
};

export type WorkspaceState = { stale: boolean; changed: string[] };

export type ReferencePage = {
  total: number;
  refs: ReferenceInfo[];
  truncated: boolean;
  limit: number;
};

export type HierarchyResult = {
  base: string[];
  interfaces: string[];
  derived: string[];
  implementations: string[];
  truncated: boolean;
};

export type SearchPage = {
  total: number;
  hits: SymbolInfo[];
  truncated: boolean;
  limit: number;
};

const MAX_HIERARCHY = 200;
const COMPACT_DOCUMENTATION = 400;
const VERBOSE_DOCUMENTATION = 800;

export class TsWorkspace {
  readonly root: string;
  readonly configPath: string;
  private program!: ts.Program;
  private checker!: ts.TypeChecker;
  private languageService!: ts.LanguageService;
  private options!: ts.CompilerOptions;
  private changed = new Set<string>();
  private stale = false;
  private watcher?: fs.FSWatcher;

  constructor(root: string, configPath?: string) {
    const absolute = path.resolve(root);
    const isFile = fs.existsSync(absolute) && fs.statSync(absolute).isFile();
    this.root = isFile ? path.dirname(absolute) : absolute;
    this.configPath = path.resolve(
      configPath ?? (isFile ? absolute : findConfig(this.root))
    );
  }

  load(): void {
    // Try to load from persistent cache first (fast path).
    const cache = new FileSystemCache({ cacheDir: path.join(this.root, ".tscache") });
    const cached = cache.load(ts, this.program, this.localSourcesMaps());
    if (cached) {
      this.program = cached.program;
      // Update our internal map of file texts from cache.
      this.setFileTextsFromCache(cached.fileTexts);
      this.checker = this.program.getTypeChecker();
      this.languageService = ts.createLanguageService(this.createHost());
      this.stale = false;
      this.changed.clear();
      this.startWatcher();
      console.log("roslyngate-ts: loaded from persistent cache");
      return;
    }

    // Fall back to full rebuild (original behavior).
    const read = ts.readConfigFile(this.configPath, ts.sys.readFile);
    if (read.error) {
      throw new Error(ts.flattenDiagnosticMessageText(read.error.messageText, "\n"));
    }

    const parsed = ts.parseJsonConfigFileContent(
      read.config,
      ts.sys,
      path.dirname(this.configPath)
    );
    if (parsed.errors.length > 0) {
      throw new Error(parsed.errors.map((error) =>
        ts.flattenDiagnosticMessageText(error.messageText, "\n")).join("\n"));
    }

    this.options = parsed.options;
    const sourceFiles = parsed.fileNames.map((f) => ts.createSourceFile(f, ts.sys.readFile(f) ?? "", ts.ScriptTarget.Latest, true));
    this.program = ts.createProgram({
      rootNames: parsed.fileNames,
      options: this.options,
      projectReferences: parsed.projectReferences
    });
    this.checker = this.program.getTypeChecker();
    this.languageService = ts.createLanguageService(this.createHost());
    this.stale = false;
    this.changed.clear();
    this.startWatcher();

    // Persist the program state for future fast starts.
    try {
      cache.save(ts, this.program, this.localSourcesMaps());
    } catch {
      // Ignore cache save failures – not critical.
    }
  }

  reload(): void {
    this.watcher?.close();
    this.load();
  }

  close(): void {
    this.watcher?.close();
  }

  state(): WorkspaceState {
    return { stale: this.stale, changed: [...this.changed].sort() };
  }

  /** Return a Map of filename → source text for all program source files. */
  private localSourcesMaps(): Map<string, string> {
    const map = new Map<string, string>();
    for (const sf of this.program.getSourceFiles()) {
      if (!sf.isDeclarationFile) {
        const name = sf.fileName.replace(/\\/g, "/");
        map.set(name, sf.getFullText());
      }
    }
    return map;
  }

  /** Populate the internal file-text map from a cached entry. */
  private setFileTextsFromCache(_cacheFileTexts: Map<string, string>): void {
    // No-op for now – the cache is only used for program reuse.
    // File texts are re-read from disk on each reload.
  }

  sourceFileCount(): number {
    return this.localSources().length;
  }

  projectCount(): number {
    return this.program.getProjectReferences()?.length ?? 0;
  }

  typescriptVersion(): string {
    return ts.version;
  }

  isFileIndexed(file: string): boolean {
    return this.program.getSourceFile(path.resolve(this.root, file)) !== undefined;
  }

  resolve(name?: string, file?: string, line?: number, column?: number): SymbolInfo[] {
    if (file) {
      const source = this.program.getSourceFile(path.resolve(this.root, file));
      if (!source) return [];
      const position = positionAt(source, (line ?? 1) - 1, (column ?? 1) - 1);
      const node = findNodeAt(source, position);
      const symbol = node ? this.checker.getSymbolAtLocation(node) : undefined;
      if (!symbol) return [];
      return this.infos(this.realSymbol(symbol));
    }

    if (!name || name.trim().length === 0) return [];
    const target = normalizeTarget(name);
    const found = new Map<string, SymbolInfo>();
    for (const source of this.localSources()) {
      const visit = (node: ts.Node): void => {
        if (ts.isIdentifier(node) && matchesTarget(node.text, target)) {
          const symbol = this.checker.getSymbolAtLocation(node);
          if (symbol) {
            for (const info of this.infos(this.realSymbol(symbol))) {
              if (matchesTarget(info.qualifiedName, target) || matchesTarget(info.name, target)) {
                found.set(keyOf(info), info);
              }
            }
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }
    return sortSymbols([...found.values()]);
  }

  references(
    symbol: SymbolInfo,
    includeDeclarations: boolean,
    offset: number,
    limit: number
  ): ReferencePage {
    const appliedLimit = Math.max(1, Math.min(limit, 200));
    const appliedOffset = Math.max(0, offset);
    const declaration = symbol.declaration;
    const source = declaration.getSourceFile();
    const start = declaration.getStart(source);
    const groups = this.languageService.findReferences(source.fileName, start) ?? [];
    const refs = new Map<string, ReferenceInfo>();

    for (const group of groups) {
      for (const reference of group.references) {
        if (!includeDeclarations && isDefinition(reference, group.definition)) continue;
        const absoluteName = absoluteFile(this.root, reference.fileName);
        const refSource = this.program.getSourceFile(absoluteName);
        if (!refSource) continue;
        const loc = refSource.getLineAndCharacterOfPosition(reference.textSpan.start);
        const file = relative(this.root, absoluteName);
        const kind = referenceKind(refSource, reference.textSpan.start, reference.isWriteAccess);
        const info = { file, line: loc.line + 1, column: loc.character + 1, kind };
        refs.set(`${file}:${info.line}:${info.column}`, info);
      }
    }

    const ordered = [...refs.values()].sort(compareReferences);
    const page = ordered.slice(appliedOffset, appliedOffset + appliedLimit);
    return {
      total: ordered.length,
      refs: page,
      truncated: appliedOffset + page.length < ordered.length,
      limit: appliedLimit
    };
  }

  hierarchy(symbol: SymbolInfo): HierarchyResult {
    const declaration = symbol.declaration;
    const target = symbolSymbol(this.checker, declaration);
    const base: string[] = [];
    const interfaces: string[] = [];
    const derived: string[] = [];
    const implementations: string[] = [];

    for (const clause of heritageClauses(declaration)) {
      for (const typeNode of clause.types) {
        const name = this.typeName(typeNode);
        if (clause.token === ts.SyntaxKind.ExtendsKeyword) base.push(name);
        if (clause.token === ts.SyntaxKind.ImplementsKeyword) interfaces.push(name);
      }
    }

    for (const source of this.localSources()) {
      const visit = (node: ts.Node): void => {
        if (ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node)) {
          const other = node.name ? this.checker.getSymbolAtLocation(node.name) : undefined;
          if (other && target && other !== target) {
            for (const clause of node.heritageClauses ?? []) {
              for (const typeNode of clause.types) {
                const baseSymbol = symbolSymbol(this.checker, typeNode.expression);
                if (baseSymbol !== target) continue;
                const info = this.infos(other)[0];
                if (!info) continue;
                if (clause.token === ts.SyntaxKind.ImplementsKeyword) {
                  implementations.push(info.qualifiedName);
                } else {
                  derived.push(info.qualifiedName);
                }
              }
            }
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }

    const result = {
      base: uniqueSorted(base),
      interfaces: uniqueSorted(interfaces),
      derived: uniqueSorted(derived),
      implementations: uniqueSorted(implementations)
    };
    const size = result.base.length + result.interfaces.length
      + result.derived.length + result.implementations.length;
    return { ...result, truncated: size > MAX_HIERARCHY };
  }

  search(
    query: string,
    kind: string | undefined,
    namespace: string | undefined,
    offset: number,
    limit: number
  ): SearchPage {
    const appliedLimit = Math.max(1, Math.min(limit, 50));
    const appliedOffset = Math.max(0, offset);
    const needle = query.trim().toLowerCase();
    const found = new Map<string, SymbolInfo>();

    for (const source of this.localSources()) {
      const visit = (node: ts.Node): void => {
        if (ts.isIdentifier(node) && node.text.toLowerCase().includes(needle)) {
          const symbol = this.checker.getSymbolAtLocation(node);
          if (!symbol) return;
          for (const info of this.infos(this.realSymbol(symbol))) {
            if (!isSearchable(info)) continue;
            if (!info.name.toLowerCase().includes(needle)
              && !info.qualifiedName.toLowerCase().includes(needle)) continue;
            if (kind && info.kind !== kind) continue;
            if (namespace && !info.namespace.startsWith(namespace)) continue;
            found.set(keyOf(info), info);
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }

    const hits = sortSymbols([...found.values()]);
    const page = hits.slice(appliedOffset, appliedOffset + appliedLimit);
    return {
      total: hits.length,
      hits: page,
      truncated: appliedOffset + page.length < hits.length,
      limit: appliedLimit
    };
  }

  compact(info: SymbolInfo): Record<string, unknown> {
    const value: Record<string, unknown> = {
      n: info.name,
      q: info.qualifiedName,
      k: info.kind,
      s: info.signature
    };
    if (info.documentation) {
      value.d = truncate(info.documentation, COMPACT_DOCUMENTATION);
    }
    value.f = info.location.file;
    value.l = info.location.line;
    return value;
  }

  verbose(info: SymbolInfo): Record<string, unknown> {
    const value: Record<string, unknown> = {
      name: info.name,
      qualifiedName: info.qualifiedName,
      kind: info.kind
    };
    if (info.namespace) value.namespace = info.namespace;
    if (info.containingType) value.containingType = info.containingType;
    value.signature = info.signature;
    if (info.documentation) {
      value.documentation = truncate(info.documentation, VERBOSE_DOCUMENTATION);
    }
    value.location = {
      file: info.location.file,
      line: info.location.line,
      column: info.location.column
    };
    return value;
  }

  compactCandidate(info: SymbolInfo): [string, string, string, string, number] {
    return [
      info.qualifiedName,
      info.kind,
      info.signature,
      info.location.file,
      info.location.line
    ];
  }

  private infos(symbol: ts.Symbol): SymbolInfo[] {
    const declarations = (symbol.declarations ?? []).filter(isRealDeclaration);
    const selected = declarations.length > 0
      ? declarations
      : symbol.valueDeclaration ? [symbol.valueDeclaration] : [];
    const infos = selected.map((declaration) => this.toInfo(symbol, declaration));
    return sortSymbols(infos);
  }

  private toInfo(symbol: ts.Symbol, declaration: ts.Declaration): SymbolInfo {
    const source = declaration.getSourceFile();
    const loc = source.getLineAndCharacterOfPosition(declaration.getStart(source));
    const qualifiedName = this.qualifiedName(declaration);
    const namespace = relative(this.root, source.fileName).replace(/\.(d\.)?[cm]?[jt]sx?$/, "");
    const containingType = containingTypeOf(declaration);
    return {
      name: symbol.getName(),
      qualifiedName,
      kind: kindOf(declaration),
      signature: signatureOf(declaration),
      documentation: documentationOf(this.checker, symbol),
      namespace,
      containingType,
      location: {
        file: relative(this.root, source.fileName),
        line: loc.line + 1,
        column: loc.character + 1
      },
      declaration
    };
  }

  private qualifiedName(declaration: ts.Declaration): string {
    const source = declaration.getSourceFile();
    const moduleName = relative(this.root, source.fileName)
      .replace(/\.(d\.)?[cm]?[jt]sx?$/, "")
      .replace(/\/index$/, "");
    const chain = nameChain(declaration);
    return [moduleName, ...chain].filter(Boolean).join(".");
  }

  private realSymbol(symbol: ts.Symbol): ts.Symbol {
    if (symbol.flags & ts.SymbolFlags.Alias) {
      try {
        return this.checker.getAliasedSymbol(symbol);
      } catch {
        return symbol;
      }
    }
    return symbol;
  }

  private typeName(typeNode: ts.ExpressionWithTypeArguments): string {
    const symbol = symbolSymbol(this.checker, typeNode.expression);
    if (symbol) {
      const info = this.infos(this.realSymbol(symbol))[0];
      if (info) return info.qualifiedName;
    }
    return typeNode.expression.getText(typeNode.getSourceFile());
  }

  private localSources(): ts.SourceFile[] {
    return this.program.getSourceFiles().filter((source) =>
      !source.isDeclarationFile
      && !source.fileName.includes("/node_modules/")
      && !source.fileName.includes("\\node_modules\\"));
  }

  private createHost(): ts.LanguageServiceHost {
    return {
      getScriptFileNames: () => this.program.getSourceFiles().map((file) => file.fileName),
      getScriptVersion: () => "0",
      getScriptSnapshot: (fileName) => {
        const text = ts.sys.readFile(fileName);
        return text === undefined ? undefined : ts.ScriptSnapshot.fromString(text);
      },
      getCurrentDirectory: () => this.root,
      getCompilationSettings: () => this.options,
      getDefaultLibFileName: (options) => ts.getDefaultLibFilePath(options),
      fileExists: ts.sys.fileExists,
      readFile: ts.sys.readFile,
      readDirectory: ts.sys.readDirectory,
      directoryExists: ts.sys.directoryExists,
      getDirectories: ts.sys.getDirectories
    };
  }

  private startWatcher(): void {
    if (process.env.ROSLYGATE_TS_WATCH === "off") return;
    try {
      this.watcher = fs.watch(this.root, { recursive: true }, (_, file) => {
        if (!file) return;
        const name = file.toString().replaceAll("\\", "/");
        if (!/\.(ts|tsx|js|jsx|mts|cts|json)$/.test(name)) return;
        if (name.includes("node_modules") || name.includes("/dist/")) return;
        this.stale = true;
        this.changed.add(name);
      });
    } catch {
      this.stale = false;
    }
  }
}

function findConfig(root: string): string {
  for (const name of ["tsconfig.json", "jsconfig.json"]) {
    const candidate = path.join(root, name);
    if (fs.existsSync(candidate)) return candidate;
  }
  throw new Error(`No tsconfig.json or jsconfig.json found in ${root}.`);
}

function relative(root: string, file: string): string {
  return path.relative(root, file).replaceAll("\\", "/");
}

function normalizeTarget(value: string): string {
  return value.trim().replace(/\(.*$/, "").replace(/^\.\//, "");
}

function matchesTarget(value: string, target: string): boolean {
  return value === target
    || value.endsWith(`.${target}`)
    || target.endsWith(`.${value}`);
}

function keyOf(info: SymbolInfo): string {
  return `${info.qualifiedName}|${info.location.file}:${info.location.line}:${info.location.column}`;
}

function sortSymbols(items: SymbolInfo[]): SymbolInfo[] {
  return items.sort((a, b) =>
    a.qualifiedName.localeCompare(b.qualifiedName)
    || a.location.file.localeCompare(b.location.file)
    || a.location.line - b.location.line
    || a.location.column - b.location.column);
}

function compareReferences(a: ReferenceInfo, b: ReferenceInfo): number {
  return a.file.localeCompare(b.file) || a.line - b.line || a.column - b.column;
}

function uniqueSorted(values: string[]): string[] {
  return [...new Set(values.filter(Boolean))].sort();
}

function positionAt(source: ts.SourceFile, line: number, column: number): number {
  const safeLine = Math.max(0, Math.min(line, source.getLineStarts().length - 1));
  const start = source.getLineStarts()[safeLine];
  const end = safeLine + 1 < source.getLineStarts().length
    ? source.getLineStarts()[safeLine + 1]
    : source.getEnd();
  return Math.max(start, Math.min(start + Math.max(0, column), end));
}

function findNodeAt(source: ts.SourceFile, position: number): ts.Node | undefined {
  let found: ts.Node | undefined;
  const visit = (node: ts.Node): void => {
    if (position >= node.getFullStart() && position <= node.getEnd()) {
      found = node;
      ts.forEachChild(node, visit);
    }
  };
  visit(source);
  return found;
}

function absoluteFile(root: string, file: string): string {
  return path.isAbsolute(file) ? file : path.resolve(root, file);
}

function isDefinition(reference: ts.ReferenceEntry, definition: ts.ReferencedSymbolDefinitionInfo): boolean {
  return path.resolve(reference.fileName) === path.resolve(definition.fileName)
    && reference.textSpan.start === definition.textSpan.start;
}

function isSearchable(info: SymbolInfo): boolean {
  if (info.kind === "parameter") return false;
  if (info.kind !== "field") return true;
  const parent = info.declaration.parent;
  return ts.isSourceFile(parent)
    || ts.isClassDeclaration(parent)
    || ts.isClassExpression(parent)
    || ts.isInterfaceDeclaration(parent);
}

function referenceKind(source: ts.SourceFile, position: number, write: boolean): string {
  const node = findNodeAt(source, position);
  const heritage = node ? findParent(node, ts.isHeritageClause) : undefined;
  if (heritage) return "inherit";
  return write ? "write" : "read";
}

function findParent<T extends ts.Node>(
  node: ts.Node,
  predicate: (value: ts.Node) => value is T
): T | undefined {
  let current: ts.Node | undefined = node;
  while (current) {
    if (predicate(current)) return current;
    current = current.parent;
  }
  return undefined;
}

function heritageClauses(declaration: ts.Declaration): ts.HeritageClause[] {
  const node = declaration as ts.ClassDeclaration | ts.InterfaceDeclaration;
  return (node.heritageClauses ?? []) as ts.HeritageClause[];
}

function symbolSymbol(checker: ts.TypeChecker, node: ts.Node): ts.Symbol | undefined {
  const name = (node as ts.NamedDeclaration).name;
  if (name) {
    const symbol = checker.getSymbolAtLocation(name);
    if (symbol) return symbol;
  }
  return checker.getSymbolAtLocation(node);
}

function containingTypeOf(declaration: ts.Declaration): string | undefined {
  const chain = nameChain(declaration);
  if (chain.length <= 1) return undefined;
  return chain.slice(0, -1).join(".");
}

function nameChain(declaration: ts.Declaration): string[] {
  const chain: string[] = [];
  let node: ts.Node | undefined = declaration;
  while (node && !ts.isSourceFile(node)) {
    const name = (node as ts.NamedDeclaration).name;
    if (name && ts.isIdentifier(name) && !chain.includes(name.text)) {
      chain.unshift(name.text);
    }
    node = node.parent;
  }
  return chain;
}

function kindOf(declaration: ts.Declaration): string {
  if (ts.isClassDeclaration(declaration)) return "class";
  if (ts.isInterfaceDeclaration(declaration)) return "interface";
  if (ts.isEnumDeclaration(declaration)) return "enum";
  if (ts.isEnumMember(declaration)) return "enum";
  if (ts.isTypeAliasDeclaration(declaration)) return "type";
  if (ts.isFunctionDeclaration(declaration)) return "method";
  if (ts.isMethodDeclaration(declaration) || ts.isMethodSignature(declaration)) return "method";
  if (ts.isConstructorDeclaration(declaration)) return "constructor";
  if (ts.isPropertyDeclaration(declaration) || ts.isPropertySignature(declaration)) return "property";
  if (ts.isGetAccessor(declaration) || ts.isSetAccessor(declaration)) return "property";
  if (ts.isVariableDeclaration(declaration)) return "field";
  if (ts.isParameter(declaration)) return "parameter";
  if (ts.isModuleDeclaration(declaration)) return "namespace";
  if (ts.isTypeParameterDeclaration(declaration)) return "type";
  return ts.SyntaxKind[declaration.kind].replace(/Declaration$/, "").toLowerCase();
}

function signatureOf(declaration: ts.Declaration): string {
  const source = declaration.getSourceFile();
  const name = (declaration as ts.NamedDeclaration).name;
  const simple = name && ts.isIdentifier(name) ? name.text : "";
  if (ts.isClassDeclaration(declaration)) {
    return `class ${simple}`;
  }
  if (ts.isInterfaceDeclaration(declaration)) {
    return `interface ${simple}`;
  }
  if (ts.isEnumDeclaration(declaration)) {
    return `enum ${simple}`;
  }
  if (ts.isTypeAliasDeclaration(declaration)) {
    return `type ${simple}`;
  }
  const header = declaration.getText(source)
    .split("\n")[0]
    .replace(/^export\s+/, "")
    .replace(/^declare\s+/, "")
    .trim();
  const withoutBody = header.split("{")[0].trim().replace(/;$/, "");
  return withoutBody.length > 0 ? withoutBody : simple;
}

function documentationOf(checker: ts.TypeChecker, symbol: ts.Symbol): string | undefined {
  try {
    const text = ts.displayPartsToString(symbol.getDocumentationComment(checker)).trim();
    return text.length > 0 ? text : undefined;
  } catch {
    return undefined;
  }
}

function truncate(value: string, maximum: number): string {
  return value.length <= maximum ? value : `${value.slice(0, maximum)}...`;
}

function isRealDeclaration(declaration: ts.Declaration): boolean {
  return !ts.isImportSpecifier(declaration)
    && !ts.isImportClause(declaration)
    && !ts.isExportSpecifier(declaration)
    && !ts.isNamespaceImport(declaration);
}

export function matchesParameterTypes(info: SymbolInfo, parameterTypes?: string[]): boolean {
  if (!parameterTypes || parameterTypes.length === 0) return true;
  const declaration = info.declaration as ts.SignatureDeclaration;
  if (!declaration.parameters || declaration.parameters.length !== parameterTypes.length) {
    return false;
  }
  return declaration.parameters.every((parameter, index) => {
    const actual = parameter.type?.getText(parameter.getSourceFile()) ?? "unknown";
    const expected = parameterTypes[index];
    return actual === expected
      || actual.endsWith(`.${expected}`)
      || actual.replaceAll(" ", "") === expected.replaceAll(" ", "");
  });
}
