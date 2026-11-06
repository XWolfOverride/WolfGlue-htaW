# Pide candidatos de icono a un servidor de imagenes con API sdapi/v1 (txt2img). Ver DOC/ICONOS.md.
#
#   powershell -ExecutionPolicy Bypass -File tools\iconos\generar.ps1 -Server http://<servidor>:5001 -Out candidatos
#   ... -Only hola_a,notas_b -PerPrompt 3
#
# Cada prompt es el motivo seguido del estilo comun de los iconos de app. Guarda <clave>_<n>_<semilla>.png.

param(
    [Parameter(Mandatory)] [string]$Server,
    [string]$Out = 'candidatos',
    [int]$PerPrompt = 2,
    [string[]]$Only
)

function Invoke-IconCandidates {
    $ErrorActionPreference = 'Stop'
    New-Item -ItemType Directory -Force $Out | Out-Null
    $style = 'flat vector style, bold vivid colors, simple shapes, centered, clean gradient background, minimalist logo, high contrast'
    $neg = 'text, letters, words, watermark, signature, photo, realistic, noisy background, clutter, frame border, blurry, lowres, jpeg artifacts'
    $prompts = [ordered]@{
        'hola_a'     = "app icon, rounded square, a friendly waving cartoon hand next to a round speech bubble, orange and pink gradient, $style"
        'hola_b'     = "app icon, rounded square, a smiling speech bubble with a small sparkle, warm orange and magenta gradient, $style"
        'markdown_a' = "app icon, rounded square, a white document sheet with bold horizontal lines and a fountain pen nib, deep blue and cyan gradient, $style"
        'markdown_b' = "app icon, rounded square, an open book with a downward arrow, blue and teal gradient, $style"
        'notas_a'    = "app icon, rounded square, a yellow sticky note with a folded corner and a pencil, green and lime gradient, $style"
        'notas_b'    = "app icon, rounded square, a notepad with a checklist and a pencil, yellow and emerald green gradient, $style"
    }
    $url = $Server.TrimEnd('/') + '/sdapi/v1/txt2img'
    foreach ($k in $prompts.Keys) {
        if ($Only -and $Only -notcontains $k) { continue }
        for ($n = 1; $n -le $PerPrompt; $n++) {
            $body = @{ prompt = $prompts[$k]; negative_prompt = $neg; width = 768; height = 768; steps = 22; cfg_scale = 6; seed = -1 } | ConvertTo-Json
            $t = Get-Date
            $r = Invoke-RestMethod -Uri $url -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 900
            $seed = ($r.info | ConvertFrom-Json).seed
            $file = Join-Path $Out ("{0}_{1}_{2}.png" -f $k, $n, $seed)
            $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($file)
            [IO.File]::WriteAllBytes($full, [Convert]::FromBase64String($r.images[0]))
            "{0}  ({1:N0} s)" -f $file, ((Get-Date) - $t).TotalSeconds
        }
    }
}

Invoke-IconCandidates
