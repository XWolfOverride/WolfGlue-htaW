/// <reference path="glue-services.d.ts" />
const $ = (id) => document.getElementById(id);
const out = (v) => { $("salida").textContent = typeof v === "string" ? v : JSON.stringify(v, null, 2); };

$("wid").textContent = glue.window.id;
$("plat").textContent = glue.platform;

glue.on("hola.hora", (h) => { $("hora").textContent = h; });

glue.on("hola.aviso", (texto, from) => {
  const li = document.createElement("li");
  li.textContent = `${from}: ${texto}`;
  $("avisos").prepend(li);
});

$("saludar").onclick = async () => out(await glue.call("hola.saludo", $("nombre").value));
$("info").onclick = async () => out(await glue.call("hola.info"));
// Servicio C# con [GlueService] (tipos en web/glue-services.d.ts, generado al depurar)
$("tareas").onclick = async () => {
  const t = await glue.services.tareas.anadir("Tarea " + new Date().toLocaleTimeString());
  await glue.services.tareas.marcar(t.id, t.id % 2 === 0);
  out(await glue.services.tareas.listar());
};
$("falla").onclick = async () => {
  try { await glue.call("hola.falla"); }
  catch (e) { out(`Error ${e.code}: ${e.message}`); }
};

$("nueva").onclick = async () => out("Abierta " + await glue.window.open("index.html", { width: 640, height: 480 }));
$("dialogo").onclick = async () => {
  const r = await glue.window.dialog("dialogo.html", { title: "Dialogo", width: 360, height: 220, resizable: false });
  out(r === null ? "Dialogo cancelado" : r);
};
$("avisar").onclick = () => {
  glue.emit("hola.aviso", "hola desde " + glue.window.id);
  glue.emit("hola.log", "aviso enviado");
};

// ---------------------------------------------------------------- servicios nativos
let ultimaRuta = null;
const recordar = (ruta) => { ultimaRuta = ruta; $("mostrar").disabled = !ruta; };

$("abrir").onclick = async () => {
  const r = await glue.dialog.open({ title: "Abrir", multiple: true,
    filters: [{ name: "Texto", patterns: ["txt", "md", "json"] }, { name: "Todos", patterns: ["*"] }] });
  out(r ?? "Cancelado");
  if (r) recordar(r[0]);
};
$("guardar").onclick = async () => {
  const r = await glue.dialog.save({ title: "Guardar como", fileName: "nota.txt", defaultExtension: "txt",
    filters: ["*.txt"] });
  out(r ?? "Cancelado");
  if (r) recordar(r);
};
$("carpeta").onclick = async () => {
  const r = await glue.dialog.folder({ title: "Elige una carpeta" });
  out(r ?? "Cancelado");
  if (r) recordar(r);
};
$("pregunta").onclick = async () =>
  out("Respuesta: " + await glue.dialog.message("¿Te gusta WolfGlue?", { kind: "question", buttons: "yesNoCancel" }));
$("copiar").onclick = async () => {
  await glue.clipboard.writeText("Copiado desde WolfGlue a las " + new Date().toLocaleTimeString());
  out("Texto copiado al portapapeles");
};
$("pegar").onclick = async () => out((await glue.clipboard.readText()) ?? "(el portapapeles no tiene texto)");
$("pantallas").onclick = async () => out(await glue.screens());
$("tema").onclick = async () => out(await glue.theme());
$("mostrar").onclick = () => ultimaRuta && glue.shell.reveal(ultimaRuta);

// Color de acento del sistema, y en vivo si cambia
const aplicarTema = (t) => document.documentElement.style.setProperty("--accent", t.accent);
glue.theme().then(aplicarTema);
glue.on("glue.theme", (t) => { aplicarTema(t); out({ temaCambiado: t }); });
