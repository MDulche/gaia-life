# Génère le certificat TLS local (mkcert) pour le reverse proxy Nginx.
param(
    [string]$HostName = $(if ($env:GAIA_HOST) { $env:GAIA_HOST } else { "gaia.local" }),
    [string]$LanIp = $env:GAIA_LAN_IP
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$CertDir = Join-Path $Root "certs"
New-Item -ItemType Directory -Force -Path $CertDir | Out-Null

function Find-Mkcert {
    $cmd = Get-Command mkcert -ErrorAction SilentlyContinue | Where-Object { $_.Source -like "*.exe" } | Select-Object -First 1
    if ($cmd) { return $cmd.Source }
    Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter mkcert.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

function Install-Mkcert {
    $existing = Find-Mkcert
    if ($existing) { return $existing }

    Write-Host "mkcert introuvable, installation via winget…"
    $null = winget install --id FiloSottile.mkcert -e --accept-package-agreements --accept-source-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
        [System.Environment]::GetEnvironmentVariable("Path", "User")
    $installed = Find-Mkcert
    if (-not $installed) {
        throw "mkcert n'est toujours pas dans le PATH. Ouvrez un nouveau terminal après l'installation, ou lancez mkcert -install en administrateur."
    }
    return $installed
}

function Get-LanIPv4 {
    Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.InterfaceAlias -notmatch "vEthernet|Loopback|Bluetooth|WSL|Default Switch|Docker|Hyper-V"
        } |
        Select-Object -ExpandProperty IPAddress -First 1
}

$mkcert = Install-Mkcert
Write-Host "mkcert : $mkcert"
& $mkcert -install

if (-not $LanIp) {
    $LanIp = Get-LanIPv4
}
if (-not $LanIp) {
    throw "Impossible de détecter l'IP LAN. Passez -LanIp ou GAIA_LAN_IP."
}

$names = @($HostName, $LanIp, "localhost", "127.0.0.1") | Select-Object -Unique
Write-Host "Noms du certificat : $($names -join ', ')"

$cert = Join-Path $CertDir "gaia.pem"
$key = Join-Path $CertDir "gaia-key.pem"
& $mkcert -cert-file $cert -key-file $key @names

$caroot = (& $mkcert -CAROOT).Trim()
Copy-Item (Join-Path $caroot "rootCA.pem") (Join-Path $CertDir "rootCA.pem") -Force

Write-Host "Certificats écrits dans $CertDir"
Write-Host "CA publique à copier sur les téléphones : $(Join-Path $CertDir 'rootCA.pem')"
Write-Host "Ne commitez jamais gaia-key.pem ni rootCA-key.pem."
