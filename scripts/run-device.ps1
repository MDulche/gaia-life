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

function Find-WifiConnectPortFromMdns {
    param(
        [string]$Adb,
        [string]$Ip
    )

    $mdns = Invoke-Adb -Adb $Adb -AdbArgs @('mdns', 'services')
    Write-Log ("adb mdns services: {0}" -f $mdns.Text)
    foreach ($line in $mdns.Lines) {
        $t = "$line".Trim()
        # Ex: adb-xxxx  _adb-tls-connect._tcp  172.16.101.247:45123
        if ($t -match [regex]::Escape($Ip) + ':(?<port>\d+)') {
            return $Matches['port']
        }
        if ($t -match '(?i)_adb.*connect|_adb\._tcp' -and $t -match ':(?<port>\d+)\s*$') {
            if ($t -match [regex]::Escape($Ip)) {
                return $Matches['port']
            }
        }
    }
    return $null
}

function Test-AdbConnectOk {
    param([string]$Text)
    return ($Text -match '(?i)connected to|already connected') `
        -and ($Text -notmatch '(?i)failed to connect|unable to connect|cannot connect|connection refused|no route|10061|refus')
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

    Write-Step 'Connexion ADB Wi-Fi...'

    if ($Ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$') {
        Stop-WithError ("IP invalide : '{0}' (attendu ex. 192.168.1.42)." -f $Ip)
    }

    $doPair = [bool]$PairCode
    if ($doPair) {
        if (-not $PairPort) {
            Stop-WithError 'Code pairing fourni mais port de pairing manquant (fenetre "Associer un appareil").'
        }
        if ($PairPort -notmatch '^\d+$') {
            Stop-WithError 'Port de pairing invalide (entier uniquement).'
        }
        if ($ConnectPort -and $PairPort -eq $ConnectPort) {
            Write-Host 'Attention : port pairing = port connexion. Sur Android 11+, ils sont souvent DIFFERENTS.' -ForegroundColor Yellow
        }
        if ($PairCode -notmatch '^\d{6}$') {
            Write-Host 'Avertissement : le code pairing Android fait en general 6 chiffres.' -ForegroundColor Yellow
        }
    }
    elseif (-not $ConnectPort) {
        Stop-WithError 'Wi-Fi : port de connexion manquant (ecran principal Debogage sans fil).'
    }

    if ($ConnectPort -and $ConnectPort -notmatch '^\d+$') {
        Stop-WithError 'Port de connexion invalide (entier uniquement).'
    }

    if ($doPair) {
        $pairTarget = '{0}:{1}' -f $Ip, $PairPort
        Write-Host ("  pair  {0}" -f $pairTarget) -ForegroundColor DarkGray
        Write-Log ("adb pair {0} ******" -f $pairTarget)
        $pair = Invoke-Adb -Adb $Adb -AdbArgs @('pair', $pairTarget, $PairCode)
        Write-Log ("adb pair out: {0}" -f $pair.Text)
        $pairOk = ($pair.Text -match 'Successfully paired') -or ($pair.ExitCode -eq 0 -and $pair.Text -notmatch '(?i)fail|error|unable')
        if (-not $pairOk) {
            Stop-WithError ("Echec adb pair. Fenetre d association ouverte ? Detail: {0}" -f $pair.Text)
        }
        Write-Ok ("Pairing OK : {0}" -f $pairTarget)

        # Apres pairing, le port de connexion affiche AVANT est souvent obsolete.
        Start-Sleep -Seconds 1
        $discovered = Find-WifiConnectPortFromMdns -Adb $Adb -Ip $Ip
        if ($discovered) {
            Write-Ok ("Port connexion detecte via mDNS : {0}" -f $discovered)
            $ConnectPort = $discovered
        }
        else {
            Write-Host ''
            Write-Host 'Pairing OK. Sur le telephone, revenez a l ecran PRINCIPAL' -ForegroundColor Cyan
            Write-Host '"Debogage sans fil" et recopiez le port ACTUEL (IP:port en haut).' -ForegroundColor Cyan
            Write-Host 'Ce port change souvent pendant / apres l association.' -ForegroundColor Cyan
            $hint = if ($ConnectPort) { $ConnectPort } else { '' }
            $prompt = if ($hint) {
                "Port CONNEXION actuel [Entree = $hint] : "
            } else {
                'Port CONNEXION actuel : '
            }
            $fresh = Read-Host $prompt
            $fresh = ($fresh -replace '\s', '').Trim()
            if ($fresh) { $ConnectPort = $fresh }
            if (-not $ConnectPort -or $ConnectPort -notmatch '^\d+$') {
                Stop-WithError 'Port de connexion requis apres pairing (ecran principal Debogage sans fil).'
            }
        }
    }
    else {
        Write-Host '  (deja associe - pairing ignore)' -ForegroundColor DarkGray
    }

    $attemptPorts = @($ConnectPort)
    $maxAttempts = 3
    $lastConnText = ''
    $connectTarget = $null

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $port = $attemptPorts[$attemptPorts.Count - 1]
        $connectTarget = '{0}:{1}' -f $Ip, $port

        Write-Log ("adb disconnect {0} (cleanup)" -f $connectTarget)
        Invoke-Adb -Adb $Adb -AdbArgs @('disconnect', $connectTarget) | Out-Null

        Write-Host ("  connect {0} (essai {1}/{2})" -f $connectTarget, $attempt, $maxAttempts) -ForegroundColor DarkGray
        Write-Log ("adb connect {0}" -f $connectTarget)
        $conn = Invoke-Adb -Adb $Adb -AdbArgs @('connect', $connectTarget)
        $lastConnText = $conn.Text
        Write-Log ("adb connect out: {0}" -f $conn.Text)

        if (Test-AdbConnectOk -Text $conn.Text) {
            break
        }

        if ($attempt -ge $maxAttempts) {
            Write-Host ''
            Write-Host 'Diagnostic (connexion refusee / 10061) :' -ForegroundColor Yellow
            Write-Host '  - Apres pairing, le port connexion change : recopiez-le sur l ecran principal.' -ForegroundColor Yellow
            Write-Host '  - Desactivez/reactivez Debogage sans fil, meme Wi-Fi (pas invite).' -ForegroundColor Yellow
            Write-Host '  - Mode 1 (USB) ou mode 4 (USB puis Wi-Fi tcpip 5555) du .bat.' -ForegroundColor Yellow
            try {
                $tnc = Test-NetConnection -ComputerName $Ip -Port ([int]$port) -WarningAction SilentlyContinue
                Write-Log ("Test-NetConnection {0}:{1} TcpTestSucceeded={2}" -f $Ip, $port, $tnc.TcpTestSucceeded)
                if (-not $tnc.TcpTestSucceeded) {
                    Write-Host ("  - Test TCP {0}:{1} = FERME." -f $Ip, $port) -ForegroundColor Yellow
                }
            }
            catch {
                Write-Log ("Test-NetConnection failed: {0}" -f $_)
            }
            Stop-WithError ("Echec adb connect vers {0}. Detail: {1}" -f $connectTarget, $lastConnText)
        }

        Write-Host ''
        Write-Host ("Echec connect sur le port {0}. Recopiez le port ACTUEL (ecran principal)." -f $port) -ForegroundColor Yellow
        $retryPort = Read-Host 'Nouveau port CONNEXION (ou vide pour abandonner)'
        $retryPort = ($retryPort -replace '\s', '').Trim()
        if (-not $retryPort -or $retryPort -notmatch '^\d+$') {
            Stop-WithError ("Echec adb connect vers {0}. Detail: {1}" -f $connectTarget, $lastConnText)
        }
        $attemptPorts += $retryPort
    }

    # Attendre le passage offline -> device
    $ready = $false
    for ($i = 0; $i -lt 12; $i++) {
        Start-Sleep -Milliseconds 500
        $rows = @(Get-AdbDeviceRows -Adb $Adb)
        $hit = @($rows | Where-Object { $_.Serial -eq $connectTarget })
        if ($hit.Count -gt 0 -and $hit[0].Status -eq 'device') {
            $ready = $true
            break
        }
        if ($i -eq 3 -or $i -eq 7) {
            Write-Log ("retry adb connect {0}" -f $connectTarget)
            Invoke-Adb -Adb $Adb -AdbArgs @('connect', $connectTarget) | Out-Null
        }
    }

    if (-not $ready) {
        $rows = @(Get-AdbDeviceRows -Adb $Adb)
        $hit = @($rows | Where-Object { $_.Serial -eq $connectTarget })
        $st = if ($hit.Count -gt 0) { $hit[0].Status } else { 'absent' }
        Stop-WithError ("Apres connect, appareil '{0}' statut '{1}' (attendu: device). Verifiez Debogage sans fil." -f $connectTarget, $st)
    }

    Write-Ok ("Connecte (device) : {0}" -f $connectTarget)
    return $connectTarget
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
