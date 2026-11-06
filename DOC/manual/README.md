# Manual de htaW

htaW abre **aplicaciones de escritorio hechas con HTML, CSS y JavaScript**: archivos `.htaw` que se abren con doble
clic como cualquier programa, con ventana propia, aspecto nativo y acceso controlado al sistema (archivos, diálogos,
procesos, Internet…).

*English version: [DOC/manual-en](../manual-en/README.md).*

Este manual tiene dos partes:

**Para usar htaW**

1. [Uso de htaW](01-uso.md): abrir aplicaciones, la ventana de permisos, el panel de htaW y la línea de comandos.

**Para crear aplicaciones htaW**

2. [Tu primera aplicación](02-primera-app.md): de un HTML vacío a una app con permisos, icono y empaquetada.
3. [El manifiesto](03-manifiesto.md): todos los campos de `htaw.json` / del bloque del HTML.
4. [La API `htaw`](04-api.md): referencia completa de lo que la app puede usar desde JavaScript, con ejemplos.
5. [Permisos y seguridad](05-permisos.md): qué concede cada permiso, cómo se comprueba y qué ve el usuario.
6. [Aspecto nativo (CSS)](06-estilo.md): la hoja de estilo de htaW, sus variables y sus clases.
7. [Ventanas y comunicación](07-ventanas.md): varias ventanas, diálogos modales con resultado y mensajes entre ventanas.
8. [Desarrollo y distribución](08-desarrollo-y-distribucion.md): modo desarrollo, depuración, `.htaw` empaquetado y
   ejecutable portable.
9. [Recetas](09-recetas.md): ejemplos completos listos para copiar.

Los ejemplos de `samples/htaw` (`hola.htaw`, `markdown.htaw` y la carpeta `notas`) son apps reales que usan lo que
se explica aquí.

> **Mantenimiento.** Este manual describe todo lo que htaW ofrece a los usuarios y a las aplicaciones. Cada vez que
> htaW gane o cambie una capacidad (un campo del manifiesto, una función de la API, un permiso, una clase CSS, una
> opción de la línea de comandos o del panel), hay que actualizar la página correspondiente **en el mismo cambio**,
> en este manual y en el inglés (`DOC/manual-en`), que tiene las mismas páginas.
> Lo que no esté aquí no existe para quien desarrolla una app.
