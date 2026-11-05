// Shim de WolfGlue: se inyecta antes de cargar cada documento. Protocolo en DOC/BRIDGE.md.
// window.__glueInit = { id, platform } lo antepone el host.
(function () {
  "use strict";
  if (window.glue) return;
  const init = window.__glueInit || {};
  delete window.__glueInit;

  const pending = new Map();
  const listeners = new Map();
  let nextId = 1;

  // ---------------------------------------------------------------- transporte segun plataforma
  let send;
  if (window.chrome && window.chrome.webview) {
    send = (text) => window.chrome.webview.postMessage(text);
    window.chrome.webview.addEventListener("message", (e) => receive(e.data));
  } else if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.glue) {
    send = (text) => window.webkit.messageHandlers.glue.postMessage(text);
  } else {
    send = () => { throw new Error("WolfGlue: no hay canal con el host"); };
  }

  function receive(text) {
    let m;
    try { m = typeof text === "string" ? JSON.parse(text) : text; } catch { return; }
    if (!m) return;
    if (m.t === "result" || m.t === "error") {
      const p = pending.get(m.id);
      if (!p) return;
      pending.delete(m.id);
      if (m.t === "result") p.resolve(m.r);
      else {
        const err = new Error(m.e && m.e.message || "Error");
        err.code = m.e && m.e.code || "Error";
        p.reject(err);
      }
    } else if (m.t === "event") {
      const set = listeners.get(m.n);
      if (set) for (const fn of [...set]) {
        try { fn(m.d, m.from); } catch (e) { console.error(e); }
      }
    }
  }

  function call(method, params) {
    const id = nextId++;
    return new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
      try { send(JSON.stringify({ t: "call", id, m: method, p: params === undefined ? null : params })); }
      catch (e) { pending.delete(id); reject(e); }
    });
  }

  function on(name, fn) {
    if (!listeners.has(name)) listeners.set(name, new Set());
    listeners.get(name).add(fn);
    return () => off(name, fn);
  }

  function off(name, fn) {
    const set = listeners.get(name);
    if (set) set.delete(fn);
  }

  function emit(name, data, options) {
    const to = options && options.to || "*";
    send(JSON.stringify({ t: "emit", n: name, d: data === undefined ? null : data, to }));
  }

  const win = Object.freeze({
    id: init.id || "",
    close: (result) => call("glue.window.close", result === undefined ? null : result),
    setTitle: (title) => call("glue.window.setTitle", String(title)),
    setSize: (width, height) => call("glue.window.setSize", { width, height }),
    center: () => call("glue.window.center"),
    minimize: () => call("glue.window.minimize"),
    maximize: () => call("glue.window.maximize"),
    restore: () => call("glue.window.restore"),
    devtools: () => call("glue.window.devtools"),
    // Ventana nueva (no modal). Devuelve su id.
    open: (url, options) => call("glue.window.open", Object.assign({ url }, options, { modal: false })).then((r) => r.id),
    // Ventana modal: se resuelve al cerrarse, con lo que se paso a glue.window.close(valor).
    dialog: (url, options) => call("glue.window.open", Object.assign({ url }, options, { modal: true }))
      .then((r) => (r.result == null ? null : JSON.parse(r.result))),
  });

  // ---------------------------------------------------------------- servicios nativos
  // Filtros: [{ name: "Texto", patterns: ["txt", "md"] }] o atajo ["*.txt", "*.md"].
  function fileOptions(o) {
    o = Object.assign({}, o);
    if (Array.isArray(o.filters))
      o.filters = o.filters.map((f) => (typeof f === "string" ? { name: f, patterns: [f] } : f));
    return o;
  }

  const dialog = Object.freeze({
    // → string[] | null
    open: (options) => call("glue.dialog.open", fileOptions(options)),
    // → string | null
    save: (options) => call("glue.dialog.save", fileOptions(options)),
    // → string | null
    folder: (options) => call("glue.dialog.folder", Object.assign({}, options)),
    // kind: info|warning|error|question · buttons: ok|okCancel|yesNo|yesNoCancel → "ok"|"cancel"|"yes"|"no"
    message: (text, options) => call("glue.dialog.message", Object.assign({ text: String(text) }, options)),
  });

  const clipboard = Object.freeze({
    readText: () => call("glue.clipboard.readText"),
    writeText: (text) => call("glue.clipboard.writeText", String(text)),
  });

  const shell = Object.freeze({
    openUrl: (url) => call("glue.shell.openUrl", String(url)),
    open: (path) => call("glue.shell.open", String(path)),
    reveal: (path) => call("glue.shell.reveal", String(path)),
  });

  window.glue = Object.freeze({
    platform: init.platform || "",
    call, on, off, emit,
    window: win,
    dialog, clipboard, shell,
    // → [{ bounds, workArea, scale, primary }]
    screens: () => call("glue.screens"),
    // → { dark, accent, fontFamily, fontSize, highContrast }; cambios con glue.on("glue.theme", fn)
    theme: () => call("glue.theme"),
    quit: (code) => call("glue.app.quit", code || 0),
    _receive: receive,
  });
})();
