#Requires -Version 5.1
<#
.SYNOPSIS
  Detecte un telephone Android physique autorise, puis build/deploye App.Mobile dessus.

.DESCRIPTION
  N'utilise jamais l'emulateur (serials emulator-*). Variables : GAIALIFE_ANDROID_SDK,
  ANDROID_HOME, ANDROID_SDK_ROOT, JAVA_HOME, GAIALIFE_DEVICE_SERIAL.
  Voir docs/DEPLOIEMENT-TELEPHONE.md
#>
[CmdletBinding()]
param(
    [string]$DeviceSerial = $(if ($env:GAIALIFE_DEVICE_SERIAL) { $env:GAIALIFE_DEVICE_SERIAL } else { '' }),
    [string]$WifiIp = '',
    [string]$WifiPairPort = '',
    [string]$WifiPairCode = '',
    [string]$WifiConnectPort = '',
    [switch]$WifiViaUsb,
    [switch]$SkipRun,
    [switch]$NoLogcat,
    [int]$LogcatSeconds = 0
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ScriptDir = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..')).Path
$ProjectPath = Join-Path $RepoRoot 'src\App.Mobile\App.Mobile.csproj'
$PackageId = 'com.gaialife.mobile'
$LogDir = Join-Path $ScriptDir '.device-logs'
$LogFile = Join-Path $LogDir ("run-device-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host ("==> {0}" -f $Message) -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host ("OK  {0}" -f $Message) -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host ("ERREUR : {0}" -f $Message) -ForegroundColor Red
}

function Write-Log {
    param([string]$Message)
    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
    Add-Content -Path $LogFile -Value ("[{0:u}] {1}" -f (Get-Date), $Message)
}

function Stop-WithError {
    param(
        [string]$Message,
        [int]$Code = 1
    )
    Write-Fail $Message
    Write-Log ("FAIL: {0}" -f $Message)
    Write-Host ("Journal : {0}" -f $LogFile) -ForegroundColor Yellow
    Write-Host 'Guide : docs\DEPLOIEMENT-TELEPHONE.md' -ForegroundColor Yellow
    exit $Code
}

function Resolve-AndroidSdk {
    $candidates = @(
        $env:GAIALIFE_ANDROID_SDK,
        $env:ANDROID_HOME,
        $env:ANDROID_SDK_ROOT,
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
        (Join-Path $env:USERPROFILE 'Android\Sdk'),
        'C:\Android\Sdk'
    ) | Where-Object { $_ -and $_.Trim() }

    foreach ($c in $candidates) {
        $full = [System.IO.Path]::GetFullPath($c)
        if (Test-Path $full) {
            return $full
        }
    }
    return $null
}

function Resolve-JavaHome {
    if ($env:JAVA_HOME -and (Test-Path $env:JAVA_HOME)) {
        return $env:JAVA_HOME
    }

    $androidOpenJdks = @(
        Get-ChildItem 'C:\Program Files\Android\openjdk' -Directory -ErrorAction SilentlyContinue
        Get-ChildItem 'C:\Program Files (x86)\Android\openjdk' -Directory -ErrorAction SilentlyContinue
    ) | Sort-Object Name -Descending
    if ($androidOpenJdks.Count -gt 0) {
        return $androidOpenJdks[0].FullName
    }

    $microsoftJdks = @(Get-ChildItem 'C:\Program Files\Microsoft' -Filter 'jdk*' -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending)
    if ($microsoftJdks.Count -gt 0) {
        return $microsoftJdks[0].FullName
    }

    $studioJbr = 'C:\Program Files\Android\Android Studio\jbr'
    if (Test-Path $studioJbr) {
        return $studioJbr
    }

    $adoptium = @(Get-ChildItem 'C:\Program Files\Eclipse Adoptium' -Filter 'jdk*' -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending)
    if ($adoptium.Count -gt 0) {
        return $adoptium[0].FullName
    }

    $javaCmd = Get-Command java -ErrorAction SilentlyContinue
    if ($javaCmd) {
        $bin = Split-Path $javaCmd.Source -Parent
        $home = Split-Path $bin -Parent
        if (Test-Path (Join-Path $home 'bin\java.exe')) {
            return $home
        }
    }

    return $null
}

function Set-JavaEnvironment {
    param([string]$JavaHome)

    $env:JAVA_HOME = $JavaHome
    $javaBin = Join-Path $JavaHome 'bin'
    if ((Test-Path $javaBin) -and ($env:PATH -notlike "*$javaBin*")) {
        $env:PATH = "$javaBin;$env:PATH"
    }
}

function Find-SdkTool {
    param(
        [string]$SdkRoot,
        [string]$RelativePath
    )
    $path = Join-Path $SdkRoot $RelativePath
    if (Test-Path $path) { return $path }
    return $null
}

function Get-AdbPath {
    param([string]$SdkRoot)

    $fromSdk = Find-SdkTool -SdkRoot $SdkRoot -RelativePath 'platform-tools\adb.exe'
    if ($fromSdk) { return $fromSdk }

    $cmd = Get-Command adb -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    if (Test-Path 'C:\platform-tools\adb.exe') {
        return 'C:\platform-tools\adb.exe'
    }
    return $null
}

function Get-AdbDeviceRows {
    param([string]$Adb)

    $raw = & $Adb devices -l 2>&1 | ForEach-Object { "$_" }
    Write-Log ("adb devices -l: {0}" -f ($raw -join ' | '))
    $rows = @()
    foreach ($line in $raw) {
        $t = "$line".Trim()
        if (-not $t -or $t -match '^List of devices') { continue }
        # serial STATUS [key:value ...]
        if ($t -match '^(?<serial>\S+)\s+(?<status>\S+)(?:\s+(?<rest>.*))?$') {
            $rows += [pscustomobject]@{
                Serial = $Matches['serial']
                Status = $Matches['status']
                Rest   = $Matches['rest']
                IsEmulator = ($Matches['serial'] -match '^emulator-\d+$')
            }
        }
    }
    return $rows
}

function Resolve-PhysicalDeviceSerial {
    param(
        [string]$Adb,
        [string]$PreferredSerial
    )

    $all = @(Get-AdbDeviceRows -Adb $Adb)
    $physical = @($all | Where-Object { -not $_.IsEmulator })
    $emulators = @($all | Where-Object { $_.IsEmulator -and $_.Status -eq 'device' })

    if ($emulators.Count -gt 0) {
        Write-Host ("Note : {0} emulateur(s) aussi present(s) - ils seront ignores." -f $emulators.Count) -ForegroundColor DarkYellow
        Write-Log ("Emulators ignored: {0}" -f (($emulators | ForEach-Object { $_.Serial }) -join ', '))
    }

    if ($PreferredSerial) {
        $match = @($all | Where-Object { $_.Serial -eq $PreferredSerial })
        if ($match.Count -eq 0) {
            Stop-WithError ("Appareil demande '{0}' introuvable dans adb devices. Verifiez le cable / adb connect." -f $PreferredSerial)
        }
        $row = $match[0]
        if ($row.IsEmulator) {
            Stop-WithError ("'{0}' est un emulateur. Ce script cible uniquement un telephone physique (ou GAIALIFE_DEVICE_SERIAL)." -f $PreferredSerial)
        }
        if ($row.Status -eq 'unauthorized') {
            Stop-WithError ("Appareil '{0}' = unauthorized. Acceptez la fenetre 'Autoriser le debogage USB' sur le telephone, puis relancez." -f $PreferredSerial)
        }
        if ($row.Status -eq 'offline') {
            Stop-WithError ("Appareil '{0}' = offline. Rebranchez, changez de cable/port, ou relancez: adb kill-server && adb start-server." -f $PreferredSerial)
        }
        if ($row.Status -ne 'device') {
            Stop-WithError ("Appareil '{0}' statut '{1}' (attendu: device)." -f $PreferredSerial, $row.Status)
        }
        return $row.Serial
    }

    $ready = @($physical | Where-Object { $_.Status -eq 'device' })
    $unauthorized = @($physical | Where-Object { $_.Status -eq 'unauthorized' })
    $offline = @($physical | Where-Object { $_.Status -eq 'offline' })

    if ($unauthorized.Count -gt 0) {
        $ids = ($unauthorized | ForEach-Object { $_.Serial }) -join ', '
        Stop-WithError ("Telephone detecte mais unauthorized ({0}). Acceptez le debogage USB sur l'ecran du telephone, puis relancez. Voir docs/DEPLOIEMENT-TELEPHONE.md" -f $ids)
    }

    if ($offline.Count -gt 0 -and $ready.Count -eq 0) {
        $ids = ($offline | ForEach-Object { $_.Serial }) -join ', '
        Stop-WithError ("Telephone offline ({0}). Rebranchez / adb kill-server. Voir docs/DEPLOIEMENT-TELEPHONE.md" -f $ids)
    }

    if ($ready.Count -eq 0) {
        Stop-WithError 'Aucun telephone physique autorise (statut device). Branchez le cable, activez le debogage USB, ou etablissez adb connect (Wi-Fi). Voir docs/DEPLOIEMENT-TELEPHONE.md'
    }

    if ($ready.Count -gt 1) {
        $list = ($ready | ForEach-Object { "  - $($_.Serial)" }) -join [Environment]::NewLine
        Stop-WithError ("Plusieurs telephones detectes. Specifiez lequel :`n{0}`nExemple : .\scripts\run-device.ps1 -DeviceSerial SERIAL`nou variable GAIALIFE_DEVICE_SERIAL." -f $list)
    }

    return $ready[0].Serial
}

function Invoke-Adb {
    param(
        [string]$Adb,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$AdbArgs
    )

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $Adb @AdbArgs 2>&1 | ForEach-Object { "$_" }
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Text     = (($out -join ' ').Trim())
            Lines    = @($out)
        }
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

function Connect-WifiAdb {
    param(
        [string]$Adb,
        [string]$Ip,
        [string]$PairPort,
        [string]$PairCode,
        [string]$ConnectPort
    )

    # Une seule fois : pair (si code) puis connect. Pas de disconnect / kill-server / re-essais.
    $Ip = ($Ip -replace '\s', '').Trim()
    $PairPort = ($PairPort -replace '\s', '').Trim()
    $PairCode = ($PairCode -replace '\s', '').Trim()
    $ConnectPort = ($ConnectPort -replace '\s', '').Trim()

    if (-not $Ip) { return $null }

    Write-Step 'Connexion ADB Wi-Fi...'

    if ($Ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$') {
        Stop-WithError ("IP invalide : '{0}'." -f $Ip)
    }

    if (-not $ConnectPort -or $ConnectPort -notmatch '^\d+$') {
        Stop-WithError 'Port manquant pour adb connect (IP:PORT affiche en haut).'
    }

    if ($PairCode) {
        if (-not $PairPort -or $PairPort -notmatch '^\d+$') {
            Stop-WithError 'Port fenetre association requis avec le code pairing.'
        }
        $pairTarget = '{0}:{1}' -f $Ip, $PairPort
        Write-Host ("  > adb pair {0} <code>" -f $pairTarget) -ForegroundColor DarkGray
        Write-Log ("adb pair {0} ******" -f $pairTarget)
        $pair = Invoke-Adb -Adb $Adb -AdbArgs @('pair', $pairTarget, $PairCode)
        Write-Log ("adb pair out: {0}" -f $pair.Text)
        if ($pair.Text -notmatch 'Successfully paired') {
            Stop-WithError ("Echec adb pair. Detail: {0}" -f $pair.Text)
        }
        Write-Ok ("pair OK : {0}" -f $pairTarget)
    }

    $connectTarget = '{0}:{1}' -f $Ip, $ConnectPort
    Write-Host ("  > adb connect {0}" -f $connectTarget) -ForegroundColor DarkGray
    Write-Log ("adb connect {0}" -f $connectTarget)
    $conn = Invoke-Adb -Adb $Adb -AdbArgs @('connect', $connectTarget)
    Write-Log ("adb connect out: {0}" -f $conn.Text)

    $ok = ($conn.Text -match '(?i)connected to|already connected') `
        -and ($conn.Text -notmatch '(?i)cannot connect|failed to connect|10061|refus')

    if ($ok) {
        Write-Ok ("connect OK : {0} (session conservee)" -f $connectTarget)
        return $connectTarget
    }

    Write-Host ''
    Write-Host ("Wi-Fi refuse : {0}" -f $conn.Text) -ForegroundColor Yellow
    Write-Host '  U = brancher USB et deployer' -ForegroundColor Cyan
    Write-Host '  Q = quitter' -ForegroundColor Cyan
    $choice = (Read-Host 'Choix [U/Q]').Trim().ToUpperInvariant()
    if ($choice -eq 'Q') {
        Stop-WithError 'Abandon Wi-Fi. Relancez en mode 1 (USB) ou 2 (connect seul).'
    }

    Write-Host 'Attente telephone USB...' -ForegroundColor Cyan
    for ($i = 0; $i -lt 45; $i++) {
        $rows = @(Get-AdbDeviceRows -Adb $Adb)
        $usb = @($rows | Where-Object {
            -not $_.IsEmulator -and $_.Status -eq 'device' -and $_.Serial -notmatch ':\d+$'
        })
        if ($usb.Count -ge 1) {
            Write-Ok ("USB OK : {0}" -f $usb[0].Serial)
            return $usb[0].Serial
        }
        Start-Sleep -Seconds 1
    }
    Stop-WithError 'Aucun USB detecte. Mode 1 avec cable + debogage USB.'
}

function Connect-WifiViaUsbTcpip {
    param(
        [string]$Adb,
        [string]$Ip,
        [int]$TcpipPort = 5555
    )

    $Ip = ($Ip -replace '\s', '').Trim()
    if ($Ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$') {
        Stop-WithError ("IP invalide : '{0}'." -f $Ip)
    }

    Write-Step 'Mode USB -> Wi-Fi (adb tcpip)...'
    Write-Host 'Branchez le cable USB, acceptez le debogage, puis attente detection...' -ForegroundColor DarkGray

    $usbSerial = $null
    for ($i = 0; $i -lt 30; $i++) {
        $rows = @(Get-AdbDeviceRows -Adb $Adb)
        $usb = @($rows | Where-Object { -not $_.IsEmulator -and $_.Status -eq 'device' -and $_.Serial -notmatch ':\d+$' })
        if ($usb.Count -eq 1) {
            $usbSerial = $usb[0].Serial
            break
        }
        if ($usb.Count -gt 1) {
            Stop-WithError ("Plusieurs telephones USB detectes. Gardez-en un seul branche, ou -DeviceSerial.")
        }
        Start-Sleep -Seconds 1
    }

    if (-not $usbSerial) {
        Stop-WithError 'Aucun telephone USB en statut device. Cable donnees + Debogage USB + autorisation a l ecran.'
    }
    Write-Ok ("USB detecte : {0}" -f $usbSerial)

    Write-Log ("adb -s {0} tcpip {1}" -f $usbSerial, $TcpipPort)
    $tcp = Invoke-Adb -Adb $Adb -AdbArgs @('-s', $usbSerial, 'tcpip', "$TcpipPort")
    Write-Log ("adb tcpip out: {0}" -f $tcp.Text)
    if ($tcp.Text -notmatch '(?i)restarting|listening' -and $tcp.ExitCode -ne 0) {
        Stop-WithError ("Echec adb tcpip. Detail: {0}" -f $tcp.Text)
    }
    Write-Ok ("tcpip {0} active sur l appareil" -f $TcpipPort)
    Start-Sleep -Seconds 2

    $connectTarget = '{0}:{1}' -f $Ip, $TcpipPort
    Write-Host ("Vous pouvez debrancher le cable. Connect Wi-Fi {0} ..." -f $connectTarget) -ForegroundColor DarkGray
    $conn = Invoke-Adb -Adb $Adb -AdbArgs @('connect', $connectTarget)
    Write-Log ("adb connect out: {0}" -f $conn.Text)
    $connOk = ($conn.Text -match '(?i)connected to|already connected') `
        -and ($conn.Text -notmatch '(?i)failed|cannot connect|refus|10061')
    if (-not $connOk) {
        Stop-WithError ("Echec connect apres tcpip vers {0}. Verifiez IP Wi-Fi du telephone. Detail: {1}" -f $connectTarget, $conn.Text)
    }

    Start-Sleep -Seconds 1
    $rows2 = @(Get-AdbDeviceRows -Adb $Adb)
    $hit = @($rows2 | Where-Object { $_.Serial -eq $connectTarget -and $_.Status -eq 'device' })
    if ($hit.Count -eq 0) {
        Stop-WithError ("Connect OK mais statut pas device pour {0}. adb devices pour verifier." -f $connectTarget)
    }

    Write-Ok ("Wi-Fi via USB pret : {0}" -f $connectTarget)
    return $connectTarget
}

function Start-FilteredLogcat {
    param(
        [string]$Adb,
        [string]$Serial,
        [int]$Seconds
    )

    Write-Step ("logcat filtre (package {0}) - Ctrl+C pour arreter" -f $PackageId)
    Write-Host ("adb -s {0} ..." -f $Serial) -ForegroundColor DarkGray

    # Attendre le process un moment apres le lancement MAUI
    $pidApp = $null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        $pidApp = (& $Adb -s $Serial shell pidof $PackageId 2>$null | Out-String).Trim()
        if ($pidApp -match '^\d+') { break }
    }

    if ($pidApp -match '^\d+') {
        Write-Ok ("Processus app pid={0}" -f $pidApp)
        if ($Seconds -gt 0) {
            & $Adb -s $Serial logcat -v time --pid=$pidApp -t $Seconds
        }
        else {
            & $Adb -s $Serial logcat -v time --pid=$pidApp
        }
        return
    }

    Write-Host 'pidof introuvable - repli sur filtre texte gaialife / AndroidRuntime / mono.' -ForegroundColor Yellow
    # Repli : stream complet filtré côté PowerShell (Ctrl+C pour quitter)
    $job = Start-Job -ScriptBlock {
        param($AdbPath, $Ser)
        & $AdbPath -s $Ser logcat -v time
    } -ArgumentList $Adb, $Serial

    try {
        $deadline = if ($Seconds -gt 0) { (Get-Date).AddSeconds($Seconds) } else { [datetime]::MaxValue }
        while ((Get-Date) -lt $deadline) {
            $lines = Receive-Job $job
            foreach ($line in $lines) {
                if ("$line" -match 'gaialife|AndroidRuntime|mono-rt|MONO|chromium|Blazor|SQLite|FATAL|Exception') {
                    Write-Host $line
                }
            }
            Start-Sleep -Milliseconds 200
        }
    }
    finally {
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
    }
}

# --- Main ----------------------------------------------------------------
Write-Host 'Gaia-Life - deploiement telephone Android + App.Mobile' -ForegroundColor White
Write-Host ("Repo : {0}" -f $RepoRoot)
Write-Log ("Start DeviceSerial='{0}' Repo={1}" -f $DeviceSerial, $RepoRoot)

if (-not (Test-Path $ProjectPath)) {
    Stop-WithError ("Projet introuvable : {0}" -f $ProjectPath)
}

Write-Step 'Verification des prerequis...'

$sdk = Resolve-AndroidSdk
if (-not $sdk) {
    $defaultSdkInstall = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    Write-Host 'SDK Android non trouve via variables env.' -ForegroundColor Yellow
    Write-Host ("Tentative InstallAndroidDependencies vers {0}..." -f $defaultSdkInstall) -ForegroundColor Yellow
    if (-not (Test-Path $defaultSdkInstall)) {
        New-Item -ItemType Directory -Path $defaultSdkInstall -Force | Out-Null
    }
    & dotnet build $ProjectPath `
        -t:InstallAndroidDependencies `
        -f net9.0-android `
        -p:AcceptAndroidSDKLicenses=True `
        -p:AndroidSdkDirectory=$defaultSdkInstall
    if ($LASTEXITCODE -ne 0) {
        Stop-WithError ("InstallAndroidDependencies a echoue (code {0})." -f $LASTEXITCODE)
    }
    $env:GAIALIFE_ANDROID_SDK = $defaultSdkInstall
    $sdk = Resolve-AndroidSdk
}

if (-not $sdk) {
    Stop-WithError 'Android SDK introuvable. Definissez GAIALIFE_ANDROID_SDK ou ANDROID_HOME. Voir docs/DEPLOIEMENT-TELEPHONE.md'
}

$env:ANDROID_HOME = $sdk
$env:ANDROID_SDK_ROOT = $sdk
Write-Ok ("Android SDK : {0}" -f $sdk)

$java = Resolve-JavaHome
if ($java) {
    Set-JavaEnvironment -JavaHome $java
    Write-Ok ("JAVA_HOME : {0}" -f $java)
}
else {
    Write-Host 'JAVA_HOME non detecte - le build dotnet peut quand meme reussir.' -ForegroundColor Yellow
}

$adb = Get-AdbPath -SdkRoot $sdk
if (-not $adb) {
    Stop-WithError ("adb introuvable. Installez platform-tools dans {0}\platform-tools." -f $sdk)
}
Write-Ok ("adb : {0}" -f $adb)

if ($WifiViaUsb) {
    if (-not $WifiIp) {
        Stop-WithError 'WifiViaUsb requiert -WifiIp (adresse Wi-Fi du telephone).'
    }
    $wifiSerial = Connect-WifiViaUsbTcpip -Adb $adb -Ip $WifiIp
    if ($wifiSerial -and -not $DeviceSerial) {
        $DeviceSerial = $wifiSerial
        Write-Log ("DeviceSerial auto (USB->Wi-Fi)={0}" -f $DeviceSerial)
    }
}
elseif ($WifiIp) {
    $wifiSerial = Connect-WifiAdb -Adb $adb -Ip $WifiIp -PairPort $WifiPairPort -PairCode $WifiPairCode -ConnectPort $WifiConnectPort
    if ($wifiSerial -and -not $DeviceSerial) {
        $DeviceSerial = $wifiSerial
        Write-Log ("DeviceSerial auto (Wi-Fi)={0}" -f $DeviceSerial)
    }
}

Write-Step 'Detection du telephone physique...'
$serial = Resolve-PhysicalDeviceSerial -Adb $adb -PreferredSerial $DeviceSerial
Write-Ok ("Cible physique : {0}" -f $serial)
Write-Log ("Selected serial={0}" -f $serial)

if ($SkipRun) {
    Write-Ok 'SkipRun : appareil OK, deploiement ignore.'
    exit 0
}

# AdbTarget = selecteur adb (ex. "-s SERIAL"), pas le serial nu.
# Ref: https://learn.microsoft.com/dotnet/android/building-apps/build-properties#adbtarget
$adbTarget = "-s $serial"

Write-Step ("Build + deploiement App.Mobile sur {0}..." -f $serial)
$dotnetArgs = @(
    'build', $ProjectPath,
    '-t:Run',
    '-f', 'net9.0-android',
    ("-p:AndroidSdkDirectory={0}" -f $sdk),
    ("-p:AdbTarget={0}" -f $adbTarget)
)
if ($java) {
    $dotnetArgs += ("-p:JavaSdkDirectory={0}" -f $java)
}

Write-Log ("dotnet {0}" -f ($dotnetArgs -join ' '))
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    Stop-WithError ("Build/deploiement en echec (code {0}). Verifiez: dotnet workload install maui. Journal: {1}" -f $LASTEXITCODE, $LogFile)
}

Write-Ok ("App.Mobile deployee et lancee sur {0}." -f $serial)
Write-Host ''
Write-Host 'SQLite embarque :' -ForegroundColor Cyan
Write-Host ('  "{0}" -s {1} shell run-as {2} ls -la files/' -f $adb, $serial, $PackageId)
Write-Host ("Journal script : {0}" -f $LogFile)
Write-Log 'Deploy success'

if (-not $NoLogcat) {
    Start-FilteredLogcat -Adb $adb -Serial $serial -Seconds $LogcatSeconds
}

exit 0
