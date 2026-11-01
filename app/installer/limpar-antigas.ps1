# ---------------------------------------------------------------------------
# Orbi — remove TODAS as instalações antigas antes de instalar a nova versão.
#
# Chamado pelo instalador na etapa "removendo versões antigas" (PrepareToInstall),
# que passa -Log para gravar o que foi feito em um arquivo visível no final.
# Cobre os casos que o Inno sozinho não cobre:
#   · instalação por usuário em %LOCALAPPDATA%\Programs\Orbi
#   · instalação antiga em Program Files (versões com UAC)
#   · cópias em outras pastas (Downloads, área de trabalho)
#   · atalhos quebrados no Menu Iniciar / Área de trabalho / Inicializar
#   · processos Orbi.exe ainda rodando (inclusive de outra sessão)
#   · entradas no registro (Run, desinstalador)
# ---------------------------------------------------------------------------
param(
    [switch]$Silencioso,
    [string]$ManterPasta = "",
    [string]$Log = ""
)

$ErrorActionPreference = "SilentlyContinue"
$saida = New-Object System.Collections.Generic.List[string]
if ([string]::IsNullOrWhiteSpace($Log)) { $Log = Join-Path $env:TEMP "orbi-limpeza.txt" }
function Log([string]$texto) {
    $script:saida.Add($texto)
    if (-not $Silencioso) { Write-Host $texto }
    try { $texto | Add-Content -LiteralPath $script:Log -Encoding UTF8 } catch { }
}

# zera o log desta execução (o instalador mostra este arquivo no final)
try {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Log) | Out-Null
    "Orbi — limpeza de versões antigas ($(Get-Date -Format 'dd/MM/yyyy HH:mm'))" |
        Set-Content -LiteralPath $Log -Encoding UTF8
} catch { }

Log "Orbi — limpeza de versões antigas"
Log ""

# 1. encerra processos de qualquer versão antiga -----------------------------
$processos = Get-Process -Name "Orbi" -ErrorAction SilentlyContinue
if ($processos) {
    foreach ($p in $processos) {
        try {
            Log ("  encerrando processo PID {0} ({1})" -f $p.Id, $p.Path)
            Stop-Process -Id $p.Id -Force
        } catch { Log ("  não consegui encerrar PID {0}" -f $p.Id) }
    }
    Start-Sleep -Milliseconds 800
} else {
    Log "  nenhum processo do Orbi rodando"
}

# 2. roda os desinstaladores encontrados ------------------------------------
$chaves = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$desinstaladores = @()
foreach ($chave in $chaves) {
    Get-ItemProperty $chave -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "Orbi*" -and $_.UninstallString } |
        ForEach-Object {
            Log ("  encontrada versão instalada: {0} ({1})" -f $_.DisplayName, $_.DisplayVersion)
            $desinstaladores += $_.UninstallString
        }
}

foreach ($cmd in $desinstaladores) {
    # UninstallString costuma vir como: "C:\...\unins000.exe"
    $exe = $cmd.Trim('"')
    if (Test-Path $exe) {
        try {
            Log ("  desinstalando: {0}" -f $exe)
            Start-Process -FilePath $exe -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -Wait
        } catch { Log ("  falha ao desinstalar {0}" -f $exe) }
    }
}
if ($desinstaladores.Count -gt 0) { Start-Sleep -Seconds 1 }

# 3. apaga pastas de instalação remanescentes -------------------------------
$pastas = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Orbi"),
    (Join-Path $env:ProgramFiles "Orbi"),
    (Join-Path ${env:ProgramFiles(x86)} "Orbi"),
    (Join-Path $env:APPDATA "Orbi\app")
)

foreach ($pasta in $pastas) {
    if ([string]::IsNullOrWhiteSpace($pasta)) { continue }
    if ($pasta -eq $ManterPasta) { continue }
    if (Test-Path $pasta) {
        try {
            Remove-Item -LiteralPath $pasta -Recurse -Force
            Log ("  pasta removida: {0}" -f $pasta)
        } catch {
            Log ("  ATENÇÃO: não consegui apagar {0} (arquivo em uso?)" -f $pasta)
        }
    }
}

# 4. atalhos quebrados / duplicados ----------------------------------------
$atalhos = @(
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Orbi"),
    (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\Orbi"),
    (Join-Path ([Environment]::GetFolderPath("Desktop")) "Orbi.lnk"),
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Orbi.lnk"),
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Iniciar\Orbi.lnk"),
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\Orbi.lnk")
)

foreach ($item in $atalhos) {
    if ([string]::IsNullOrWhiteSpace($item)) { continue }
    if (Test-Path $item) {
        try {
            Remove-Item -LiteralPath $item -Recurse -Force
            Log ("  atalho antigo removido: {0}" -f $item)
        } catch { Log ("  não consegui remover o atalho {0}" -f $item) }
    }
}

# 5. entradas de registro ---------------------------------------------------
$runKeys = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
)
foreach ($k in $runKeys) {
    $valor = (Get-ItemProperty $k -Name "Orbi" -ErrorAction SilentlyContinue).Orbi
    if ($valor) {
        Remove-ItemProperty -Path $k -Name "Orbi" -Force -ErrorAction SilentlyContinue
        Log "  entrada de inicialização antiga removida do registro"
    }
}

Log ""
Log "Limpeza concluída.
         Você pode apagar este arquivo."
