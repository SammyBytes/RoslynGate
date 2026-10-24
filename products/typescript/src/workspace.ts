import fs from "node:fs";
import path from "node:path";
import ts from "typescript";

export type SymbolLocation = { file: string; line: number; column: number };
export type SymbolInfo = {
  name: string;
  qualifiedName: string;
  kind: string;
  signature: string;
  location: SymbolLocation;
  declaration: ts.Declaration;
};
export type ReferenceInfo = { file: string; line: number; kind: string };
export type WorkspaceState = { stale: boolean; changed: string[] };

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
      throw new Error(parsed.errors.map((e) => ts.flattenDiagnosticMessageText(e.messageText, "\n")).join("\n"));
    }

    this.options = parsed.options;
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

  sourceFileCount(): number {
    return this.program.getSourceFiles().filter((file) => !file.isDeclarationFile).length;
  }

  resolve(name?: string, file?: string, line?: number, column?: number): SymbolInfo[] {
    if (file) {
      const source = this.program.getSourceFile(path.resolve(this.root, file));
      if (!source) return [];
      const position = source.getPositionOfLineAndCharacter((line ?? 1) - 1, (column ?? 1) - 1);
      const node = findNodeAt(source, position);
      const symbol = node ? this.checker.getSymbolAtLocation(node) : undefined;
      return symbol ? [this.toInfo(symbol)] : [];
    }

    if (!name) return [];
    const simple = name.split(".").pop()!;
    const found = new Map<string, SymbolInfo>();
    for (const source of this.program.getSourceFiles()) {
      if (source.isDeclarationFile || source.fileName.includes("/node_modules/")) continue;
      const visit = (node: ts.Node): void => {
        if (ts.isIdentifier(node) && node.text === simple) {
          const symbol = this.checker.getSymbolAtLocation(node);
          if (symbol && hasRealDeclaration(symbol)) {
            const info = this.toInfo(symbol);
            if (matchesName(info.qualifiedName, name)) {
              found.set(`${info.location.file}:${info.location.line}:${info.location.column}`, info);
            }
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }
    return [...found.values()].sort((a, b) =>
      a.location.file.localeCompare(b.location.file)
      || a.location.line - b.location.line
      || a.location.column - b.location.column);
  }

  references(symbol: SymbolInfo, offset: number, limit: number): {
    total: number; refs: ReferenceInfo[]; truncated: boolean;
  } {
    const declaration = symbol.declaration;
    const source = declaration.getSourceFile();
    const start = declaration.getStart(source);
    const groups = this.languageService.findReferences(source.fileName, start) ?? [];
    const refs = new Map<string, ReferenceInfo>();
    for (const group of groups) {
      for (const ref of group.references) {
        const refSource = this.program.getSourceFile(ref.fileName);
        if (!refSource) continue;
        const loc = refSource.getLineAndCharacterOfPosition(ref.textSpan.start);
        const file = relative(this.root, ref.fileName);
        const key = `${file}:${loc.line + 1}`;
        if (!refs.has(key)) {
          refs.set(key, { file, line: loc.line + 1, kind: kindOf(ref) });
        }
      }
    }
    const ordered = [...refs.values()].sort((a, b) =>
      a.file.localeCompare(b.file) || a.line - b.line);
    const page = ordered.slice(offset, offset + limit);
    return { total: ordered.length, refs: page, truncated: offset + page.length < ordered.length };
  }

  hierarchy(symbol: SymbolInfo): { base: string[]; interfaces: string[]; derived: string[]; implementations: string[] } {
    const declaration = symbol.declaration;
    const type = this.checker.getTypeAtLocation(declaration);
    const target = (declaration as ts.ClassDeclaration).name
      ? this.checker.getSymbolAtLocation((declaration as ts.ClassDeclaration).name!)
      : type.symbol;
    const base: string[] = [];
    const interfaces: string[] = [];
    const derived: string[] = [];
    const implementations: string[] = [];

    for (const clause of (declaration as ts.ClassDeclaration).heritageClauses ?? []) {
      for (const typeNode of clause.types) {
        const name = typeNode.expression.getText(typeNode.getSourceFile());
        if (clause.token === ts.SyntaxKind.ExtendsKeyword) base.push(name);
        if (clause.token === ts.SyntaxKind.ImplementsKeyword) interfaces.push(name);
      }
    }

    for (const source of this.program.getSourceFiles()) {
      if (source.isDeclarationFile || source.fileName.includes("/node_modules/")) continue;
      const visit = (node: ts.Node): void => {
        if (ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node)) {
          const other = node.name ? this.checker.getSymbolAtLocation(node.name) : undefined;
          if (other && other !== target) {
            for (const clause of node.heritageClauses ?? []) {
              for (const typeNode of clause.types) {
                const baseSymbol = this.checker.getSymbolAtLocation(typeNode.expression);
                if (baseSymbol !== target) continue;
                if (clause.token === ts.SyntaxKind.ImplementsKeyword) {
                  implementations.push(other.getName());
                } else {
                  derived.push(other.getName());
                }
              }
            }
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }
    return {
      base,
      interfaces,
      derived: [...new Set(derived)].sort(),
      implementations: [...new Set(implementations)].sort()
    };
  }

  search(query: string, offset: number, limit: number): { total: number; hits: SymbolInfo[]; truncated: boolean } {
    const found = new Map<string, SymbolInfo>();
    for (const source of this.program.getSourceFiles()) {
      if (source.isDeclarationFile || source.fileName.includes("/node_modules/")) continue;
      const visit = (node: ts.Node): void => {
        if (ts.isIdentifier(node) && node.text.toLowerCase().includes(query.toLowerCase())) {
          const symbol = this.checker.getSymbolAtLocation(node);
          if (symbol && hasRealDeclaration(symbol)) {
            const info = this.toInfo(symbol);
            found.set(`${info.qualifiedName}|${info.location.file}:${info.location.line}`, info);
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }
    const hits = [...found.values()].sort((a, b) => a.qualifiedName.localeCompare(b.qualifiedName));
    const page = hits.slice(offset, offset + limit);
    return { total: hits.length, hits: page, truncated: offset + page.length < hits.length };
  }

  private toInfo(symbol: ts.Symbol): SymbolInfo {
    const declaration = symbol.declarations?.find(isRealDeclaration) ?? symbol.valueDeclaration;
    if (!declaration) throw new Error(`No declaration for ${symbol.getName()}`);
    const source = declaration.getSourceFile();
    const loc = source.getLineAndCharacterOfPosition(declaration.getStart(source));
    return {
      name: symbol.getName(),
      qualifiedName: this.qualifiedName(symbol),
      kind: ts.SyntaxKind[declaration.kind],
      signature: declaration.getText(source).split("\n")[0].trim(),
      location: { file: relative(this.root, source.fileName), line: loc.line + 1, column: loc.character + 1 },
      declaration
    };
  }

  private qualifiedName(symbol: ts.Symbol): string {
    try { return this.checker.getFullyQualifiedName(symbol); }
    catch { return symbol.getName(); }
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
        const name = file.toString();
        if (!/\.(ts|tsx|js|jsx|json|mts|cts)$/.test(name)) return;
        if (name.includes("node_modules") || name.includes("dist/")) return;
        this.stale = true;
        this.changed.add(name.replaceAll("\\", "/"));
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

function matchesName(qualified: string, expected: string): boolean {
  return qualified === expected || qualified.endsWith(`.${expected}`);
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

function isRealDeclaration(declaration: ts.Declaration): boolean {
  return !ts.isImportSpecifier(declaration)
    && !ts.isImportClause(declaration)
    && !ts.isExportSpecifier(declaration)
    && !ts.isNamespaceImport(declaration);
}

function hasRealDeclaration(symbol: ts.Symbol): boolean {
  return (symbol.declarations ?? []).some(isRealDeclaration);
}

function kindOf(ref: ts.ReferenceEntry): string {
  return ref.isWriteAccess ? "write" : "read";
}
