const $ = (id) => document.getElementById(id);
const filtros = [{ name: "Texto", patterns: ["txt", "md"] }, { name: "Todos", patterns: ["*"] }];
let ruta = null;
let sucio = false;

function actualizar() {
  $("archivo").textContent = ruta || "Sin título";
  $("estado").textContent = sucio ? "● modificado" : "";
  $("cuenta").textContent = `${$("texto").value.length} caracteres`;
  $("mostrar").disabled = !ruta;
  htaw.window.setTitle((sucio ? "● " : "") + (ruta ? ruta.split(/[\\/]/).pop() : "Sin título") + " — Notas");
}

async function cargar(r) {
  $("texto").value = await htaw.fs.readText(r);
  ruta = r;
  sucio = false;
  await htaw.settings.set("ultimo", r);
  actualizar();
}

async function guardar(como) {
  let destino = ruta;
  if (como || !destino) {
    destino = await htaw.dialog.save({ title: "Guardar nota", fileName: "nota.txt", defaultExtension: "txt", filters: filtros });
    if (!destino) return;
  }
  await htaw.fs.writeText(destino, $("texto").value);
  ruta = destino;
  sucio = false;
  await htaw.settings.set("ultimo", destino);
  actualizar();
}

async function seguirSiHayCambios() {
  if (!sucio) return true;
  const r = await htaw.dialog.message("Hay cambios sin guardar. ¿Guardarlos?", { kind: "question", buttons: "yesNoCancel" });
  if (r === "cancel") return false;
  if (r === "yes") await guardar(false);
  return !sucio;
}

$("texto").addEventListener("input", () => { sucio = true; actualizar(); });
$("nuevo").onclick = async () => {
  if (!(await seguirSiHayCambios())) return;
  $("texto").value = ""; ruta = null; sucio = false; actualizar();
};
$("abrir").onclick = async () => {
  if (!(await seguirSiHayCambios())) return;
  const r = await htaw.dialog.open({ title: "Abrir nota", filters: filtros });
  if (r) await cargar(r[0]);
};
$("guardar").onclick = () => guardar(false);
$("guardarComo").onclick = () => guardar(true);
$("mostrar").onclick = () => ruta && htaw.shell.reveal(ruta);

document.addEventListener("keydown", (e) => {
  if (!e.ctrlKey) return;
  const k = e.key.toLowerCase();
  if (k === "s") { e.preventDefault(); guardar(e.shiftKey); }
  if (k === "o") { e.preventDefault(); $("abrir").click(); }
  if (k === "n") { e.preventDefault(); $("nuevo").click(); }
});

// Archivo pasado por argumento, o el ultimo que se abrio.
(async () => {
  const inicial = htaw.app.args[0] || await htaw.settings.get("ultimo");
  if (inicial && await htaw.fs.exists(inicial)) await cargar(inicial);
  else actualizar();
  $("texto").focus();
})();
