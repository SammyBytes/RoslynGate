/**
 * Persistent Cache for TypeScript Language Service
 *
 * Saves/reloads the compiled program state to/from disk so that
 * subsequent starts of roslyngate-ts are faster (no full re-compilation).
 *
 * Format: JSON file with version, compilerOptions, file snapshots, timestamp.
 *
 * Usage:
 *   const cache = new FileSystemCache({ cacheDir: "./.tscache" });
 *   // Before loading workspace:
 *   cache.load(ts, program, fileTexts);  // returns updated program if valid cache
 *   // After loading and using workspace:
 *   cache.save(ts, program, fileTexts);
 */

import * as ts from "typescript";
import * as fs from "node:fs";
import * as path from "node:path";

const DEFAULT_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours
const CACHE_FILENAME = "program-cache.json";

/**
 * Configuration options for the file system cache.
 */
interface CacheOptions {
  cacheDir: string;
  ttlMs?: number;
  maxSize?: number;
}

/**
 * Cached data structure persisted to disk.
 */
interface CacheData {
  version: string;
  compilerOptions: ts.CompilerOptions;
  fileSnapshots: Record<string, string>;
  lastAccess: number;
}

/**
 * Main cache class responsible for persisting TypeScript program state.
 */
export class FileSystemCache {
  private cacheDir: string;
  private ttlMs: number;
  private maxSize: number;

  /**
   * Create a new cache instance.
   *
   * @param options.cacheDir   Directory where the cache file will be stored.
   * @param options.ttlMs      Time-to-live in milliseconds (default: 24h).
   * @param options.maxSize    Maximum number of entries (kept for future use).
   */
  constructor(options: CacheOptions) {
    this.cacheDir = options.cacheDir;
    this.ttlMs = options.ttlMs ?? DEFAULT_TTL_MS;
    this.maxSize = options.maxSize ?? 1000;

    // Ensure the cache directory exists.
    fs.mkdirSync(this.cacheDir, { recursive: true });
  }

  /**
   * Load the cached program state if it exists and is still valid.
   *
   * @param ts                                   The TypeScript module.
   * @param existingProgram                      Existing program to potentially reuse.
   * @param fileTexts                            Current file texts map.
   * @returns {{ program: import("typescript").Program, fileTexts: Map<string, string> } | null}
   *          Updated program and file texts from cache, or null if no valid cache.
   */
  load(
    ts: typeof import("typescript"),
    existingProgram: import("typescript").Program | undefined,
    fileTexts: Map<string, string>
  ): {
    program: import("typescript").Program;
    fileTexts: Map<string, string>;
  } | null {
    const cacheFile = path.join(this.cacheDir, CACHE_FILENAME);
    if (!fs.existsSync(cacheFile)) {
      return null;
    }

    try {
      const content = fs.readFileSync(cacheFile, "utf8");
      const data: CacheData = JSON.parse(content);

      // Basic validation: check required fields.
      if (
        data.version !== undefined &&
        data.compilerOptions !== undefined &&
        data.fileSnapshots !== undefined &&
        data.lastAccess !== undefined
      ) {
        const age = Date.now() - data.lastAccess;
        if (age > this.ttlMs) {
          // Cache expired – remove it so we don’t keep using stale data.
          this.invalidate();
          return null;
        }

        // Reconstruct source files from the cached snapshots.
        const sourceFiles = this.rebuildSourceFiles(ts, data.fileSnapshots);
        if (sourceFiles === null) {
          return null;
        }

        // Create a new program from the cached source files and options.
        const program = this.createProgram(ts, sourceFiles, data.compilerOptions);
        if (!program) {
          return null;
        }

        return {
          program,
          fileTexts: new Map(Object.entries(data.fileSnapshots)),
        };
      } else {
        // Malformed cache – treat as miss and remove it.
        this.invalidate();
        return null;
      }
    } catch (error) {
      // Corrupt cache file – ignore and start fresh.
      console.error(`Cache read error: ${error}`);
      this.invalidate();
      return null;
    }
  }

  /**
   * Save the current program state to the cache file.
   *
   * @param ts          The TypeScript module.
   * @param program     The TypeScript program to cache.
   * @param fileTexts   Map of fileName → source text.
   */
  save(ts: typeof import("typescript"), program: import("typescript").Program, fileTexts: Map<string, string>) {
    const cacheFile = path.join(this.cacheDir, CACHE_FILENAME);

    // Build the data object to persist.
    const data: CacheData = {
      version: ts.version,
      compilerOptions: program.getCompilerOptions(),
      fileSnapshots: this.mapToPlainObject(fileTexts),
      lastAccess: Date.now(),
    };

    try {
      fs.writeFileSync(cacheFile, JSON.stringify(data, null, 2), "utf8");
    } catch (error) {
      console.error(`Cache write error: ${error}`);
    }
  }

  /**
   * Invalidate (delete) the cache file.
   */
  invalidate() {
    const cacheFile = path.join(this.cacheDir, CACHE_FILENAME);
    try {
      if (fs.existsSync(cacheFile)) {
        fs.unlinkSync(cacheFile);
      }
    } catch (error) {
      console.error(`Cache invalidation error: ${error}`);
    }
  }

  /**
   * Convert a Map<string, string> to a plain JSON-compatible object.
   */
  private mapToPlainObject(map: Map<string, string>): Record<string, string> {
    const obj: Record<string, string> = {};
    for (const [key, value] of map) {
      obj[key] = value;
    }
    return obj;
  }

  /**
   * Rebuild TypeScript SourceFile objects from cached snapshots.
   *
   * @param ts               The TypeScript module.
   * @param snapshots        { fileName: text, ... }
   * @returns {import("typescript").SourceFile[] | null}  Array of source files or null on failure.
   */
  private rebuildSourceFiles(
    ts: typeof import("typescript"),
    snapshots: Record<string, string>
  ): import("typescript").SourceFile[] | null {
    try {
      const sourceFiles: import("typescript").SourceFile[] = [];
      const rootNames: string[] = [];

      for (const [fileName, text] of Object.entries(snapshots)) {
        const sf = ts.createSourceFile(
          fileName,
          text,
          ts.ScriptTarget.Latest,
          true // setParentNodes
        );
        sourceFiles.push(sf);
        rootNames.push(fileName);
      }

      return sourceFiles;
    } catch (error) {
      console.error(
        `Error rebuilding source files from cache: ${error}`
      );
      return null;
    }
  }

  /**
   * Create a new TypeScript Program from the given source files and options.
   *
   * @param ts
   * @param sourceFiles
   * @param compilerOptions
   * @returns {import("typescript").Program | null}
   */
  private createProgram(
    ts: typeof import("typescript"),
    sourceFiles: import("typescript").SourceFile[],
    compilerOptions: import("typescript").CompilerOptions
  ): import("typescript").Program | null {
    try {
      // Merge default options with provided ones.
      const defaultOptions: import("typescript").CompilerOptions = {
        target: ts.ScriptTarget.ES2022,
        module: ts.ModuleKind.CommonJS,
        strict: true,
        skipLibCheck: true,
        esModuleInterop: true,
        forceConsistentCasingInFileNames: true,
        ...compilerOptions,
      };

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const program: import("typescript").Program = ts.createProgram(
        sourceFiles as any,
        defaultOptions
      );
      return program;
    } catch (error) {
      console.error(
        `Error creating TypeScript program from cache: ${error}`
      );
      return null;
    }
  }
}