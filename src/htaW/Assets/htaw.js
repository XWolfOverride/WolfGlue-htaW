// API htaw.* (DOC/HTAW-API.md). Se inyecta en cada documento despues del shim de WolfGlue (window.glue).
// window.__htawInit lo antepone htaW con los datos de la app.
(function () {
  "use strict";
  if (window.htaw || !window.glue) return;
  const init = window.__htawInit || {};
  delete window.__htawInit;
  const call = glue.call;

  for (const w of init.warnings || []) console.warn("[htaW] " + w);

  // ---------------------------------------------------------------- tema y data-os
  // El documento aun no existe al ejecutarse este script: se espera a <html>.
  function whenRoot(fn) {
    if (document.documentElement) { fn(document.documentElement); return; }
    new MutationObserver((_, obs) => {
      if (document.documentElement) { obs.disconnect(); fn(document.documentElement); }
    }).observe(document, { childList: true });
  }

  function contrastText(hex) {
    const m = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex || "");
    if (!m) return "#ffffff";
    const [r, g, b] = m.slice(1).map((x) => {
      const c = parseInt(x, 16) / 255;
      return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
    });
    return 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.4 ? "#000000" : "#ffffff";
  }

  // htaW ya manda las variables del tema en el HTML (<style id="htaw-theme"> detras de htaw.css);
  // aqui solo se actualizan si el tema cambia con la app abierta.
  function applyTheme(t) {
    const style = document.getElementById("htaw-theme");
    if (!t || !style) return;
    style.textContent = ":root{--htaw-accent:" + t.accent + ";--htaw-accent-text:" + contrastText(t.accent) +
      ";--htaw-font:\"" + t.fontFamily + "\",system-ui,sans-serif}";
  }
  whenRoot((root) => { root.dataset.os = init.os || ""; });

  const theme = Object.assign({ dark: matchMedia("(prefers-color-scheme: dark)").matches, os: init.os || "" }, init.theme || {});
  const themeListeners = new Set();
  glue.on("glue.theme", (t) => {
    Object.assign(theme, t);
    applyTheme(t); // solo actua si esta el <style id="htaw-theme"> (theme "native")
    for (const fn of [...themeListeners]) { try { fn(theme); } catch (e) { console.error(e); } }
  });

  // ---------------------------------------------------------------- eventos con id (procesos, vigilancia)
  // Los eventos pueden llegar antes de que el JS conozca el id: se guardan un momento.
  function idRouter(eventName) {
    const handlers = new Map();
    const early = new Map();
    glue.on(eventName, (e) => {
      const h = handlers.get(e.id);
      if (h) h(e);
      else {
        if (!early.has(e.id)) early.set(e.id, []);
        early.get(e.id).push(e);
      }
    });
    return {
      add(id, fn) {
        handlers.set(id, fn);
        for (const e of early.get(id) || []) fn(e);
        early.delete(id);
      },
      remove(id) { handlers.delete(id); },
    };
  }
  const procRouter = idRouter("htaw.proc");
  const watchRouter = idRouter("htaw.watch");

  // ---------------------------------------------------------------- utilidades
  function fileOptions(o) {
    o = Object.assign({}, o);
    if (Array.isArray(o.filters))
      o.filters = o.filters.map((f) => (typeof f === "string" ? { name: f, patterns: [f] } : f));
    return o;
  }

  function httpError(status, text) {
    const e = new Error(text || ("HTTP " + status));
    e.code = status === 403 ? "PermissionDenied" : status === 404 ? "NotFound" : status === 400 ? "InvalidArgument" : "IOError";
    return e;
  }

  function toBase64(bytes) {
    let s = "";
    for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    return btoa(s);
  }

  const fsUrl = (p) => "/_htaw/fs?path=" + encodeURIComponent(p);

  // ---------------------------------------------------------------- API
  const app = Object.freeze({
    id: init.app?.id || "",
    name: init.app?.name || "",
    version: init.app?.version || null,
    args: Object.freeze([...(init.app?.args || [])]),
    dir: init.app?.dir || null,
    file: init.app?.file || "",
    dev: !!init.dev,
    quit: (code) => glue.quit(code || 0),
    relaunch: () => call("htaw.app.relaunch"),
  });

  const win = Object.freeze({
    get id() { return glue.window.id; },
    setTitle: glue.window.setTitle,
    setSize: glue.window.setSize,
    center: glue.window.center,
    minimize: glue.window.minimize,
    maximize: glue.window.maximize,
    restore: glue.window.restore,
    close: glue.window.close,
    // → { id, result }  (result: Promise con lo que la otra pagina pase a htaw.window.close(valor); null si no es modal)
    async open(page, options) {
      options = Object.assign({}, options);
      if (options.modal) return { id: null, result: glue.window.dialog(page, options) };
      return { id: await glue.window.open(page, options), result: null };
    },
  });

  const dialog = Object.freeze({
    open: (o) => call("htaw.dialog.open", fileOptions(o)),
    save: (o) => call("htaw.dialog.save", fileOptions(o)),
    folder: (o) => call("htaw.dialog.folder", Object.assign({}, o)),
    message: (text, o) => call("htaw.dialog.message", Object.assign({ text: String(text) }, o)),
  });

  const fs = Object.freeze({
    readText: (path) => call("htaw.fs.readText", String(path)),
    writeText: (path, text, o) => call("htaw.fs.writeText", { path: String(path), text: String(text), append: !!(o && o.append) }),
    async readBytes(path) {
      const r = await fetch(fsUrl(path));
      if (!r.ok) throw httpError(r.status, await r.text());
      return new Uint8Array(await r.arrayBuffer());
    },
    async writeBytes(path, data) {
      const body = data instanceof Uint8Array ? data : new Uint8Array(data);
      const r = await fetch(fsUrl(path), { method: "POST", body });
      if (!r.ok) throw httpError(r.status, await r.text());
    },
    list: (path) => call("htaw.fs.list", String(path)),
    stat: (path) => call("htaw.fs.stat", String(path)),
    exists: (path) => call("htaw.fs.exists", String(path)),
    mkdir: (path) => call("htaw.fs.mkdir", String(path)),
    remove: (path, o) => call("htaw.fs.remove", { path: String(path), recursive: !!(o && o.recursive) }),
    rename: (from, to, o) => call("htaw.fs.rename", { from: String(from), to: String(to), overwrite: !!(o && o.overwrite) }),
    copy: (from, to, o) => call("htaw.fs.copy", { from: String(from), to: String(to), overwrite: !!(o && o.overwrite) }),
    // fn({ type: change|create|delete|rename, path, oldPath }) → { close() }
    async watch(path, fn, o) {
      const id = await call("htaw.fs.watch", { path: String(path), recursive: !!(o && o.recursive) });
      watchRouter.add(id, (e) => fn({ type: e.type, path: e.path, oldPath: e.oldPath }));
      return { close: () => { watchRouter.remove(id); return call("htaw.fs.unwatch", id); } };
    },
  });

  function execArgs(name, args, o) {
    return Object.assign({ name: String(name), args: (args || []).map(String) }, o);
  }

  const shell = Object.freeze({
    open: (target) => call("htaw.shell.open", String(target)),
    reveal: (path) => call("htaw.shell.reveal", String(path)),
    // → { code, stdout, stderr }
    exec: (name, args, o) => call("htaw.shell.exec", execArgs(name, args, o)),
    // → proceso: on("stdout"|"stderr"|"exit", fn), write(texto), closeInput(), kill(), exited (Promise<codigo>)
    async spawn(name, args, o) {
      const id = await call("htaw.shell.spawn", execArgs(name, args, o));
      const listeners = { stdout: new Set(), stderr: new Set(), exit: new Set() };
      let resolveExit;
      const exited = new Promise((r) => { resolveExit = r; });
      procRouter.add(id, (e) => {
        const value = e.type === "exit" ? e.code : e.data;
        for (const fn of [...(listeners[e.type] || [])]) { try { fn(value); } catch (err) { console.error(err); } }
        if (e.type === "exit") { procRouter.remove(id); resolveExit(e.code); }
      });
      return Object.freeze({
        id,
        exited,
        on(type, fn) { listeners[type]?.add(fn); return this; },
        write: (data) => call("htaw.shell.write", { id, data: String(data) }),
        closeInput: () => call("htaw.shell.closeInput", id),
        kill: () => call("htaw.shell.kill", id),
      });
    },
  });

  const settings = Object.freeze({
    get: async (key, fallback) => { const v = await call("htaw.settings.get", String(key)); return v === null || v === undefined ? fallback ?? null : v; },
    set: (key, value) => call("htaw.settings.set", { key: String(key), value: value === undefined ? null : value }),
    remove: (key) => call("htaw.settings.remove", String(key)),
    all: () => call("htaw.settings.all"),
  });

  const http = Object.freeze({
    // → { status, statusText, ok, headers, body, json() }
    async fetch(url, o) {
      o = Object.assign({}, o);
      const args = { url: String(url), method: o.method || "GET", headers: Object.assign({}, o.headers), responseType: o.responseType, timeout: o.timeout };
      if (o.body instanceof Uint8Array || o.body instanceof ArrayBuffer) args.bodyBase64 = toBase64(new Uint8Array(o.body));
      else if (o.body !== undefined && o.body !== null && typeof o.body === "object") {
        args.body = JSON.stringify(o.body);
        if (!Object.keys(args.headers).some((k) => k.toLowerCase() === "content-type")) args.headers["Content-Type"] = "application/json";
      } else if (o.body !== undefined && o.body !== null) args.body = String(o.body);
      const r = await call("htaw.http.fetch", args);
      r.ok = r.status >= 200 && r.status < 300;
      r.json = () => JSON.parse(r.body);
      return r;
    },
  });

  const clipboard = Object.freeze({
    readText: () => call("htaw.clipboard.readText"),
    writeText: (text) => call("htaw.clipboard.writeText", String(text)),
  });

  const env = Object.freeze(Object.assign({
    get: (name) => call("htaw.env.get", String(name)),
  }, init.env || {}));

  const permissions = Object.freeze([...(init.permissions || [])]);

  window.htaw = Object.freeze({
    app,
    window: win,
    dialog, fs, shell, settings, http, clipboard, env,
    theme: Object.assign(theme, {
      on(type, fn) { if (type === "change") themeListeners.add(fn); return () => themeListeners.delete(fn); },
    }),
    permissions,
    has: (p) => permissions.includes(p) || (p === "shell.exec" && permissions.some((x) => x.startsWith("shell.exec:"))),
  });
})();
