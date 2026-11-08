<#
.SYNOPSIS
  Cliente del canal de control de depuracion de WolfGlue (solo compilaciones Debug; ver DOC/COMPILACION-Y-PRUEBAS.md).

.DESCRIPTION
  La app se arranca con WOLFGLUE_DEBUG_CONTROL=1 (por ejemplo: $env:WOLFGLUE_DEBUG_CONTROL = "1"; .\run.cmd --dev samples\htaw\fondo).
  Este script se conecta a su tuberia, manda un comando y escribe la respuesta JSON. Sin -Pid usa wolfglue-debug.pipe del
  directorio actual (el nombre de la tuberia de la ultima ejecucion).

.EXAMPLE
  .\tools\debug\wg.ps1 estado
  .\tools\debug\wg.ps1 eval "document.title"
  .\tools\debug\wg.ps1 eval "await htaw.window.setBackdrop('blurred'); htaw.theme.backdrop"
  .\tools\debug\wg.ps1 captura -File C:\temp\ventana.png
  .\tools\debug\wg.ps1 pixel -X 10 -Y 90 -W 100 -H 20
#>
param(
  [Parameter(Mandatory = $true, Position = 0)][ValidateSet("estado", "eval", "captura", "pixel")][string]$Cmd,
  [Parameter(Position = 1)][string]$Js,
  [int]$ProcessId = 0,
  [string]$Window,
  [string]$File,
  [string]$Mode,
  [int]$X = 0, [int]$Y = 0, [int]$W = 1, [int]$H = 1,
  [int]$Timeout = 10000
)

if ($ProcessId -gt 0) { $name = "wolfglue-debug-$ProcessId" }
elseif (Test-Path "wolfglue-debug.pipe") { $name = (Get-Content "wolfglue-debug.pipe" -Raw).Trim() }
else { Write-Error "No hay wolfglue-debug.pipe en $(Get-Location); arranca la app con WOLFGLUE_DEBUG_CONTROL=1 o usa -ProcessId."; exit 1 }

$req = @{ cmd = $Cmd }
if ($Window) { $req.window = $Window }
switch ($Cmd) {
  "eval"    { $req.js = $Js; $req.timeout = $Timeout }
  "captura" { if ($File) { $req.file = $File }; if ($Mode) { $req.mode = $Mode } }
  "pixel"   { $req.x = $X; $req.y = $Y; $req.w = $W; $req.h = $H; if ($Mode) { $req.mode = $Mode } }
}

$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $name, [System.IO.Pipes.PipeDirection]::InOut)
try { $pipe.Connect(3000) } catch { Write-Error "No se pudo conectar a $name (la app sigue abierta y es Debug?)"; exit 1 }
$utf8 = New-Object System.Text.UTF8Encoding($false)
$writer = New-Object System.IO.StreamWriter($pipe, $utf8); $writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($pipe, $utf8)
$writer.WriteLine(($req | ConvertTo-Json -Compress))
$reader.ReadLine()
$pipe.Dispose()
