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

  // Confirmacion de cierre: cada fn(), sincrona o con promesa, puede devolver false para que la ventana siga abierta.
  // Solo cuando el cierre lo pide el usuario o el sistema; glue.window.close() desde la pagina no pregunta.
  const closingHandlers = new Set();
  on("glue.closing", async () => {
    call("glue.window.closeAck").catch(() => {});
    let close = true;
    for (const fn of [...closingHandlers]) {
      try { if ((await fn()) === false) { close = false; break; } } catch (e) { console.error(e); }
    }
    call("glue.window.closeReply", close).catch(() => {});
  });
  function onClosing(fn) {
    if (typeof fn !== "function") throw new TypeError("onClosing necesita una funcion");
    closingHandlers.add(fn);
    if (closingHandlers.size === 1) call("glue.window.guardClose", true).catch(() => {});
    return () => {
      if (closingHandlers.delete(fn) && closingHandlers.size === 0) call("glue.window.guardClose", false).catch(() => {});
    };
  }

  const win = Object.freeze({
    id: init.id || "",
    onClosing,
    close: (result) => call("glue.window.close", result === undefined ? null : result),
    setTitle: (title) => call("glue.window.setTitle", String(title)),
    setSize: (width, height) => call("glue.window.setSize", { width, height }),
    center: () => call("glue.window.center"),
    minimize: () => call("glue.window.minimize"),
    maximize: () => call("glue.window.maximize"),
    restore: () => call("glue.window.restore"),
    devtools: () => call("glue.window.devtools"),
    // Barra de titulo propia: set(definicion | null), update(id, cambios), focus(id),
    // on("click" | "input" | "submit", fn(id, { button, value })) → funcion para dejar de escuchar
    titlebar: Object.freeze({
      set: (bar) => call("glue.window.titlebar.set", bar == null ? null : bar),
      update: (id, changes) => call("glue.window.titlebar.update", { id: String(id), changes: changes || {} }),
      focus: (id) => call("glue.window.titlebar.focus", String(id)),
      on: (type, fn) => on("glue.titlebar", (d) => { if (d.type === type) fn(d.id, { button: d.button ?? null, value: d.value ?? null }); }),
    }),
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

  // Imagenes del portapapeles: viajan como PNG en base64. Se acepta Blob/File, ArrayBuffer, Uint8Array, <img>, <canvas>,
  // ImageBitmap o URL data:; lo que no sea ya PNG se convierte con un canvas.
  const image = Object.freeze({
    async toPngBase64(data) {
      if (typeof data === "string") data = await (await fetch(data)).blob();
      if (data instanceof ArrayBuffer || ArrayBuffer.isView(data)) data = new Blob([data]);
      let png;
      if (data instanceof Blob) {
        const head = new Uint8Array(await data.slice(0, 8).arrayBuffer());
        if (head[0] === 0x89 && head[1] === 0x50 && head[2] === 0x4e && head[3] === 0x47) png = data;
        else data = await createImageBitmap(data);
      }
      if (!png) {
        const w = data.naturalWidth || data.width, h = data.naturalHeight || data.height;
        const c = new OffscreenCanvas(w, h);
        c.getContext("2d").drawImage(data, 0, 0);
        png = await c.convertToBlob({ type: "image/png" });
      }
      const bytes = new Uint8Array(await png.arrayBuffer());
      let s = "";
      for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
      return btoa(s);
    },
    fromBase64(b64) {
      if (b64 == null) return null;
      const s = atob(b64), bytes = new Uint8Array(s.length);
      for (let i = 0; i < s.length; i++) bytes[i] = s.charCodeAt(i);
      return new Blob([bytes], { type: "image/png" });
    },
  });

  const clipboard = Object.freeze({
    readText: () => call("glue.clipboard.readText"),
    writeText: (text) => call("glue.clipboard.writeText", String(text)),
    // → Blob (image/png) | null
    readImage: async () => image.fromBase64(await call("glue.clipboard.readImage")),
    writeImage: async (data) => call("glue.clipboard.writeImage", await image.toPngBase64(data)),
  });

  const shell = Object.freeze({
    openUrl: (url) => call("glue.shell.openUrl", String(url)),
    open: (path) => call("glue.shell.open", String(path)),
    reveal: (path) => call("glue.shell.reveal", String(path)),
  });

  // Servicios C# con [GlueService]: glue.services.<servicio>.<metodo>(...args) → call("servicio.metodo", [args])
  const services = new Proxy({}, {
    get: (_, svc) => typeof svc !== "string" ? undefined : new Proxy({}, {
      get: (_, m) => typeof m !== "string" || m === "then" ? undefined : (...args) => call(svc + "." + m, args),
    }),
  });

  window.glue = Object.freeze({
    platform: init.platform || "",
    call, on, off, emit, services,
    window: win,
    dialog, clipboard, shell,
    // → [{ bounds, workArea, scale, primary }]
    screens: () => call("glue.screens"),
    // → { dark, accent, fontFamily, fontSize, highContrast }; cambios con glue.on("glue.theme", fn)
    theme: () => call("glue.theme"),
    quit: (code) => call("glue.app.quit", code || 0),
    _receive: receive,
    _image: image,
  });
})();
