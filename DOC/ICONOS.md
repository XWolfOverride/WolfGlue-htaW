# Iconos de las apps de ejemplo

Los iconos de `samples/htaw` se generaron con un servidor de imágenes por IA (API `sdapi/v1`, `txt2img`) y se
convirtieron a `.ico` con una herramienta propia. Todo está en `tools/iconos/`:

| Archivo | Qué es |
|---|---|
| `generar.ps1` | Pide candidatos al servidor: dos prompts por icono, `-PerPrompt` imágenes de cada uno. |
| `mkico.cs` | Aplicación de un solo archivo de .NET 10: recorte, esquinas redondeadas y `.ico`. |
| `originales/` | Candidato elegido de cada icono, tal como salió del servidor. |

## Estilo común

Todos los iconos de app siguen el mismo estilo: cuadrado redondeado, vector plano y colores vivos, con un motivo
sencillo y una gama de color propia para cada app (así no se confunden en la barra de tareas).

- Prompt: `app icon, rounded square, <motivo>, <colores> gradient, flat vector style, bold vivid colors, simple
  shapes, centered, clean gradient background, minimalist logo, high contrast`.
- Negativo: `text, letters, words, watermark, signature, photo, realistic, noisy background, clutter, frame border,
  blurry, lowres, jpeg artifacts`.
- 768×768, 22 pasos, CFG 6, muestreador por defecto, semilla aleatoria.

## Cómo se hizo

1. Candidatos:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\iconos\generar.ps1 -Server http://<servidor>:5001 -Out candidatos
   ```

2. Se eligió uno por app (el nombre del original lleva la clave del prompt, el número y la semilla).

3. Conversión: se recorta el cuadrado redondeado del original (un margen igual en cada borde, p. ej. `0.225`, o un
   cuadro `x,y,lado`) y se redondean las esquinas con un radio del 17 % del lado:

   ```powershell
   dotnet run tools/iconos/mkico.cs -- tools/iconos/originales/<original>.png samples/htaw/<app>.ico <vista-256>.png <recorte> 0.17
   ```

El `.ico` lleva 16, 20, 24, 32, 40, 48, 64, 96, 128 y 256 px. Hasta 128 son DIB de 32 bits con alfa y el de 256 es
un PNG. Cada tamaño sale de reducir a la mitad, una y otra vez, un maestro de 1024 px, para que los pequeños no salgan
dentados. htaW elige del `.ico` el tamaño más cercano al que pide el sistema.

## Iconos

| App | Icono | Original |
|---|---|---|
| Hola (`samples/htaw/hola.ico`) | Bocadillo sonriente, naranja y magenta | `hola_b_1_144592.png` (cuadro `104,104,550`) |
| Markdown (`samples/htaw/markdown.ico`) | Hoja con pluma, azul y cian | `markdown_a_1_748482.png` (cuadro `92,88,590`: la hoja asoma por arriba) |
| Notas (`samples/htaw/notas/icono.ico`) | Nota adhesiva con lápiz, amarillo y verde | `notas_a_2_363418.png` (cuadro `164,162,440`) |

El cuadro (`x,y,lado` en píxeles del original) se usa cuando el objeto no está centrado; se mide sobre el original
con una cuadrícula. Si el motivo cabe centrado, basta el recorte simétrico (`0.225`).
