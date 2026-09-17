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

function Connect-WifiAdb {
    param(
        [string]$Adb,
        [string]$Ip,
        [string]$PairPort,
        [string]$PairCode,
        [string]$ConnectPort
    )

    $Ip = ($Ip -replace '\s', '').Trim()
    $PairPort = ($PairPort -replace '\s', '').Trim()
    $PairCode = ($PairCode -replace '\s', '').Trim()
    $ConnectPort = ($ConnectPort -replace '\s', '').Trim()

    if (-not $Ip) {
        return $null
    }

    Write-Step 'Connexion ADB Wi-Fi (pairing + connect)...'

    if (-not $PairPort -or -not $PairCode) {
        Stop-WithError 'Wi-Fi : IP renseignee mais port de pairing ou code manquant. Sur le telephone : Debogage sans fil > Associer avec un code.'
    }
    if (-not $ConnectPort) {
        Stop-WithError 'Wi-Fi : port de connexion manquant (affiche sous Debogage sans fil, souvent different du port de pairing).'
    }
    if ($Ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$') {
        Stop-WithError ("IP invalide : '{0}' (attendu ex. 192.168.1.42)." -f $Ip)
    }
    if ($PairPort -notmatch '^\d+$' -or $ConnectPort -notmatch '^\d+$') {
        Stop-WithError 'Ports de pairing / connexion invalides (entiers uniquement).'
    }
    if ($PairCode -notmatch '^\d{6}$') {
        Write-Host 'Avertissement : le code pairing Android fait en general 6 chiffres.' -ForegroundColor Yellow
    }

    $pairTarget = '{0}:{1}' -f $Ip, $PairPort
    $connectTarget = '{0}:{1}' -f $Ip, $ConnectPort

    Write-Host ("  pair  {0}" -f $pairTarget) -ForegroundColor DarkGray
    Write-Log ("adb pair {0} ******" -f $pairTarget)
    $pairOut = & $Adb pair $pairTarget $PairCode 2>&1 | ForEach-Object { "$_" }
    $pairText = ($pairOut -join ' ').Trim()
    Write-Log ("adb pair out: {0}" -f $pairText)
    if ($LASTEXITCODE -ne 0 -and $pairText -notmatch 'Successfully paired') {
        Stop-WithError ("Echec adb pair ({0}). Verifiez IP/port/code (fenetre d association ouverte sur le telephone). Detail: {1}" -f $LASTEXITCODE, $pairText)
    }
    Write-Ok ("Pairing OK : {0}" -f $pairTarget)

    Write-Host ("  connect {0}" -f $connectTarget) -ForegroundColor DarkGray
    Write-Log ("adb connect {0}" -f $connectTarget)
    $connOut = & $Adb connect $connectTarget 2>&1 | ForEach-Object { "$_" }
    $connText = ($connOut -join ' ').Trim()
    Write-Log ("adb connect out: {0}" -f $connText)
    if ($LASTEXITCODE -ne 0 -and $connText -notmatch 'connected to') {
        Stop-WithError ("Echec adb connect ({0}). Detail: {1}" -f $LASTEXITCODE, $connText)
    }
    Write-Ok ("Connecte : {0}" -f $connectTarget)
    Start-Sleep -Seconds 1
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

if ($WifiIp) {
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
