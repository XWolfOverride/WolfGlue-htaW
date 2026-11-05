// Tipos de la API de htaW para el editor: copiar junto a la app y referenciar con
//   /// <reference path="htaw.d.ts" />
// Documentacion: DOC/HTAW-API.md

interface HtawError extends Error {
  code: "PermissionDenied" | "NotFound" | "AccessDenied" | "Timeout" | "Cancelled" | "InvalidArgument" | "IOError" | "Error" | string;
}

interface HtawFileFilter { name: string; patterns: string[] }
interface HtawFileDialogOptions {
  title?: string;
  filters?: (HtawFileFilter | string)[];
  defaultPath?: string;
  fileName?: string;
  defaultExtension?: string;
  multiple?: boolean;
}
interface HtawMessageOptions {
  title?: string;
  kind?: "info" | "warning" | "error" | "question";
  buttons?: "ok" | "okCancel" | "yesNo" | "yesNoCancel";
  defaultButton?: number;
}
interface HtawFsEntry { name: string; path: string; isDirectory: boolean; size: number; modified: string }
interface HtawFsStat { exists: boolean; isDirectory: boolean; size: number; modified: string; created: string; readOnly: boolean }
interface HtawExecOptions { cwd?: string; env?: Record<string, string>; timeout?: number; encoding?: string; input?: string }
interface HtawExecResult { code: number; stdout: string; stderr: string }
interface HtawProcess {
  readonly id: number;
  readonly exited: Promise<number>;
  on(type: "stdout" | "stderr", fn: (text: string) => void): HtawProcess;
  on(type: "exit", fn: (code: number) => void): HtawProcess;
  write(text: string): Promise<void>;
  closeInput(): Promise<void>;
  kill(): Promise<void>;
}
interface HtawHttpOptions {
  method?: string;
  headers?: Record<string, string>;
  body?: string | Uint8Array | ArrayBuffer | object;
  responseType?: "text" | "base64";
  timeout?: number;
}
interface HtawHttpResponse {
  status: number; statusText: string; ok: boolean;
  headers: Record<string, string>; body: string; json(): any;
}
interface HtawTheme {
  dark: boolean; accent: string; fontFamily: string; fontSize: number; highContrast: boolean; os: string;
  on(type: "change", fn: (theme: HtawTheme) => void): () => void;
}

declare const htaw: {
  app: {
    id: string; name: string; version: string | null; args: readonly string[];
    dir: string | null; file: string; dev: boolean;
    quit(code?: number): Promise<void>;
    relaunch(): Promise<void>;
  };
  window: {
    readonly id: string;
    setTitle(title: string): Promise<void>;
    setSize(width: number, height: number): Promise<void>;
    center(): Promise<void>; minimize(): Promise<void>; maximize(): Promise<void>; restore(): Promise<void>;
    close(result?: any): Promise<void>;
    open(page: string, options?: { title?: string; width?: number; height?: number; modal?: boolean; resizable?: boolean }):
      Promise<{ id: string | null; result: Promise<any> | null }>;
  };
  dialog: {
    open(options?: HtawFileDialogOptions): Promise<string[] | null>;
    save(options?: HtawFileDialogOptions): Promise<string | null>;
    folder(options?: { title?: string; defaultPath?: string }): Promise<string | null>;
    message(text: string, options?: HtawMessageOptions): Promise<"ok" | "cancel" | "yes" | "no">;
  };
  fs: {
    readText(path: string): Promise<string>;
    writeText(path: string, text: string, options?: { append?: boolean }): Promise<void>;
    readBytes(path: string): Promise<Uint8Array>;
    writeBytes(path: string, data: Uint8Array | ArrayBuffer): Promise<void>;
    list(path: string): Promise<HtawFsEntry[]>;
    stat(path: string): Promise<HtawFsStat>;
    exists(path: string): Promise<boolean>;
    mkdir(path: string): Promise<void>;
    remove(path: string, options?: { recursive?: boolean }): Promise<void>;
    rename(from: string, to: string, options?: { overwrite?: boolean }): Promise<void>;
    copy(from: string, to: string, options?: { overwrite?: boolean }): Promise<void>;
    watch(path: string, fn: (e: { type: "change" | "create" | "delete" | "rename"; path: string; oldPath?: string }) => void,
      options?: { recursive?: boolean }): Promise<{ close(): Promise<void> }>;
  };
  shell: {
    open(urlOrPath: string): Promise<void>;
    reveal(path: string): Promise<void>;
    exec(name: string, args?: string[], options?: HtawExecOptions): Promise<HtawExecResult>;
    spawn(name: string, args?: string[], options?: Omit<HtawExecOptions, "timeout" | "input">): Promise<HtawProcess>;
  };
  settings: {
    get<T = any>(key: string, fallback?: T): Promise<T>;
    set(key: string, value: any): Promise<void>;
    remove(key: string): Promise<void>;
    all(): Promise<Record<string, any>>;
  };
  http: { fetch(url: string, options?: HtawHttpOptions): Promise<HtawHttpResponse> };
  clipboard: { readText(): Promise<string | null>; writeText(text: string): Promise<void> };
  env: { get(name: string): Promise<string | null>; home?: string; temp?: string; documents?: string; os?: string; arch?: string };
  theme: HtawTheme;
  permissions: readonly string[];
  has(permission: string): boolean;
};
