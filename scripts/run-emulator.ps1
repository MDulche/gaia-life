#Requires -Version 5.1
<#
.SYNOPSIS
  Cree (si besoin), demarre l'emulateur GaiaLifeTest, puis build/deploye App.Mobile.

.DESCRIPTION
  Idempotent. Variables : GAIALIFE_ANDROID_SDK, ANDROID_HOME, ANDROID_SDK_ROOT, JAVA_HOME,
  GAIALIFE_AVD_NAME (defaut GaiaLifeTest), GAIALIFE_SYSTEM_IMAGE.
  Voir docs/EMULATEUR-ANDROID.md
#>
[CmdletBinding()]
param(
    [string]$AvdName = $(if ($env:GAIALIFE_AVD_NAME) { $env:GAIALIFE_AVD_NAME } else { 'GaiaLifeTest' }),
    [switch]$SkipRun,
    [int]$BootTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ScriptDir = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..')).Path
$ProjectPath = Join-Path $RepoRoot 'src\App.Mobile\App.Mobile.csproj'
$LogDir = Join-Path $ScriptDir '.emulator-logs'
$LogFile = Join-Path $LogDir ("run-emulator-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))

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

    # JDK fourni par le workload / tooling Android (.NET)
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

    # java.exe deja sur le PATH
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

function New-SdkProcess {
    param(
        [string]$FileName,
        [string]$Arguments,
        [string]$StdInText = $null
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FileName
    $psi.Arguments = $Arguments
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if ($env:JAVA_HOME) {
        $psi.EnvironmentVariables['JAVA_HOME'] = $env:JAVA_HOME
    }
    if ($env:ANDROID_HOME) {
        $psi.EnvironmentVariables['ANDROID_HOME'] = $env:ANDROID_HOME
        $psi.EnvironmentVariables['ANDROID_SDK_ROOT'] = $env:ANDROID_SDK_ROOT
    }

    $p = [System.Diagnostics.Process]::Start($psi)
    if ($null -ne $StdInText) {
        $p.StandardInput.Write($StdInText)
    }
    $p.StandardInput.Close()
    $stdout = $p.StandardOutput.ReadToEnd()
    $stderr = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    return [pscustomobject]@{
        ExitCode = $p.ExitCode
        StdOut   = $stdout
        StdErr   = $stderr
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

function Test-MauiCliAvailable {
    return [bool](Get-Command maui -ErrorAction SilentlyContinue)
}

function Get-AvdList {
    param([string]$EmulatorExe)

    $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    $avdIniDir = Join-Path $env:USERPROFILE '.android\avd'
    if (Test-Path $avdIniDir) {
        Get-ChildItem $avdIniDir -Filter '*.ini' -File -ErrorAction SilentlyContinue | ForEach-Object {
            [void]$names.Add([System.IO.Path]::GetFileNameWithoutExtension($_.Name))
        }
    }

    if (Test-MauiCliAvailable) {
        try {
            $out = & maui android emulator list 2>&1 | ForEach-Object { "$_" }
            Write-Log ("maui emulator list: {0}" -f ($out -join '|'))
            foreach ($line in $out) {
                $t = "$line".Trim()
                if ($t -and $t -notmatch '^(Name|----|Available|ID)') { [void]$names.Add($t) }
            }
        }
        catch {
            Write-Log ("maui list failed: {0}" -f $_)
        }
    }

    if ($EmulatorExe -and (Test-Path $EmulatorExe)) {
        $out = & $EmulatorExe -list-avds 2>&1 | ForEach-Object { "$_" }
        Write-Log ("emulator -list-avds: {0}" -f ($out -join '|'))
        foreach ($line in $out) {
            $t = "$line".Trim()
            if ($t) { [void]$names.Add($t) }
        }
    }

    return @($names)
}

function Test-AvdExists {
    param([string]$Name)
    $ini = Join-Path $env:USERPROFILE ('.android\avd\{0}.ini' -f $Name)
    if (Test-Path $ini) { return $true }
    $listed = @(Get-AvdList -EmulatorExe $null)
    return ($listed -contains $Name)
}

function Resolve-EmulatorExe {
    param([string]$SdkRoot)

    $exe = Find-SdkTool -SdkRoot $SdkRoot -RelativePath 'emulator\emulator.exe'
    if ($exe) { return $exe }

    $cmd = Get-Command emulator -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Resolve-AvdManager {
    param([string]$SdkRoot)

    $avdmanager = Find-SdkTool -SdkRoot $SdkRoot -RelativePath 'cmdline-tools\latest\bin\avdmanager.bat'
    if ($avdmanager) { return $avdmanager }

    $altAvd = Get-ChildItem (Join-Path $SdkRoot 'cmdline-tools') -Recurse -Filter 'avdmanager.bat' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($altAvd) { return $altAvd.FullName }
    return $null
}

function Test-EmulatorDevicePresent {
    param([string]$Adb)

    $devices = & $Adb devices 2>&1 | Out-String
    Write-Log ("adb devices: {0}" -f $devices)
    return [bool]($devices -match 'emulator-\d+\s+device')
}

function Wait-BootCompleted {
    param(
        [string]$Adb,
        [int]$TimeoutSec
    )

    Write-Step ("Attente du boot complet (max {0}s)..." -f $TimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)

    & $Adb wait-for-device | Out-Null

    while ((Get-Date) -lt $deadline) {
        try {
            $boot = (& $Adb shell getprop sys.boot_completed 2>$null | Out-String).Trim()
            if ($boot -eq '1') {
                Start-Sleep -Seconds 3
                Write-Ok 'Emulateur pret (sys.boot_completed=1).'
                return
            }
        }
        catch {
            Write-Log ("boot poll: {0}" -f $_)
        }
        Start-Sleep -Seconds 2
    }

    Stop-WithError ("Timeout boot {0}s. Verifiez Hyper-V/WHPX et les logs sous {1}." -f $TimeoutSec, $LogDir)
}

function Initialize-GaiaAvd {
    param(
        [string]$Name,
        [string]$SdkRoot,
        [string]$EmulatorExe,
        [string]$AvdManager
    )

    Write-Step ("Verification de l AVD '{0}'..." -f $Name)
    if (Test-AvdExists -Name $Name) {
        Write-Ok ("AVD '{0}' deja present." -f $Name)
        return
    }

    Write-Host ("AVD '{0}' introuvable - creation..." -f $Name) -ForegroundColor Yellow

    if (Test-MauiCliAvailable) {
        Write-Log ("Creating via maui android emulator create --name {0}" -f $Name)
        & maui android emulator create --name $Name
        if ($LASTEXITCODE -ne 0) {
            Stop-WithError ("Echec maui android emulator create (code {0}). Essayez: maui android sdk install emulator" -f $LASTEXITCODE)
        }
        if (Test-AvdExists -Name $Name) {
            Write-Ok ("AVD '{0}' cree via maui CLI." -f $Name)
            return
        }
        Stop-WithError ("maui OK mais AVD '{0}' absent de la liste." -f $Name)
    }

    if (-not $AvdManager) {
        Stop-WithError 'Impossible de creer l AVD: ni CLI maui ni avdmanager. Voir docs/EMULATEUR-ANDROID.md'
    }

    $systemImage = if ($env:GAIALIFE_SYSTEM_IMAGE) {
        $env:GAIALIFE_SYSTEM_IMAGE
    }
    else {
        'system-images;android-35;google_apis;x86_64'
    }

    $sdkmanager = Find-SdkTool -SdkRoot $SdkRoot -RelativePath 'cmdline-tools\latest\bin\sdkmanager.bat'
    if (-not $sdkmanager) {
        $alt = Get-ChildItem (Join-Path $SdkRoot 'cmdline-tools') -Recurse -Filter 'sdkmanager.bat' -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($alt) { $sdkmanager = $alt.FullName }
    }

    if ($sdkmanager) {
        Write-Host ("Installation eventuelle image: {0}" -f $systemImage) -ForegroundColor Yellow
        Write-Log ("sdkmanager {0}" -f $systemImage)
        $yes = ('y' + [Environment]::NewLine) * 80
        $result = New-SdkProcess -FileName $sdkmanager -Arguments ('--sdk_root="{0}" "{1}" "emulator" "platform-tools"' -f $SdkRoot, $systemImage) -StdInText $yes
        Write-Log ("sdkmanager exit {0}" -f $result.ExitCode)
        Write-Log ("sdkmanager out: {0}" -f $result.StdOut)
        Write-Log ("sdkmanager err: {0}" -f $result.StdErr)
        if ($result.ExitCode -ne 0) {
            $combined = ($result.StdOut + $result.StdErr)
            if ($combined -match 'JAVA_HOME') {
                Stop-WithError 'sdkmanager exige JAVA_HOME. Installez un JDK 17+ ou definissez JAVA_HOME. Voir docs/EMULATEUR-ANDROID.md'
            }
            Write-Host ("sdkmanager a echoue (code {0}) - on tente quand meme avdmanager." -f $result.ExitCode) -ForegroundColor Yellow
        }
    }
    else {
        Write-Host 'sdkmanager introuvable - tentative avdmanager avec image deja presente.' -ForegroundColor Yellow
    }

    Write-Log ("avdmanager create avd -n {0} -k {1}" -f $Name, $systemImage)
    $createArgs = ('create avd -n "{0}" -k "{1}" -d pixel_6 --force' -f $Name, $systemImage)
    $result2 = New-SdkProcess -FileName $AvdManager -Arguments $createArgs -StdInText ('no' + [Environment]::NewLine)
    Write-Log ("avdmanager out: {0}" -f $result2.StdOut)
    Write-Log ("avdmanager err: {0}" -f $result2.StdErr)
    Write-Log ("avdmanager exit {0}" -f $result2.ExitCode)

    if (Test-AvdExists -Name $Name) {
        Write-Ok ("AVD '{0}' cree via avdmanager." -f $Name)
        return
    }

    if ($result2.ExitCode -eq 0 -and (Test-Path (Join-Path $env:USERPROFILE ('.android\avd\{0}.ini' -f $Name)))) {
        Write-Ok ("AVD '{0}' cree (fichier .ini present)." -f $Name)
        return
    }

    $detail = (($result2.StdOut + $result2.StdErr) -replace '\s+', ' ').Trim()
    if (-not $detail) { $detail = "exit $($result2.ExitCode)" }
    Stop-WithError ("Echec creation AVD '{0}' (image {1}). Detail: {2}. Voir docs/EMULATEUR-ANDROID.md" -f $Name, $systemImage, $detail)
}

function Start-GaiaAvd {
    param(
        [string]$Name,
        [string]$EmulatorExe,
        [string]$Adb
    )

    if (Test-EmulatorDevicePresent -Adb $Adb) {
        Write-Ok 'Un emulateur est deja lance (adb device present).'
        return
    }

    Write-Step ("Demarrage de l emulateur '{0}'..." -f $Name)

    if (Test-MauiCliAvailable) {
        Write-Log ("maui android emulator start --name {0}" -f $Name)
        Start-Process -FilePath 'maui' -ArgumentList @('android', 'emulator', 'start', '--name', $Name) -WindowStyle Normal
    }
    elseif ($EmulatorExe) {
        Write-Log ("Starting {0} -avd {1}" -f $EmulatorExe, $Name)
        Start-Process -FilePath $EmulatorExe -ArgumentList @('-avd', $Name, '-netdelay', 'none', '-netspeed', 'full') -WindowStyle Normal
    }
    else {
        Stop-WithError 'Impossible de demarrer: ni maui ni emulator.exe.'
    }

    Wait-BootCompleted -Adb $Adb -TimeoutSec $BootTimeoutSeconds
}

# --- Main ----------------------------------------------------------------
Write-Host 'Gaia-Life - lancement emulateur + App.Mobile' -ForegroundColor White
Write-Host ("Repo : {0}" -f $RepoRoot)
Write-Log ("Start AvdName={0} Repo={1}" -f $AvdName, $RepoRoot)

if (-not (Test-Path $ProjectPath)) {
    Stop-WithError ("Projet introuvable : {0}" -f $ProjectPath)
}

Write-Step 'Verification des prerequis...'

$defaultSdkInstall = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$sdk = Resolve-AndroidSdk
if (-not $sdk) {
    Write-Host 'SDK Android non trouve via variables env.' -ForegroundColor Yellow
    Write-Host ("Tentative InstallAndroidDependencies vers {0}..." -f $defaultSdkInstall) -ForegroundColor Yellow
    Write-Log ("Running InstallAndroidDependencies -> {0}" -f $defaultSdkInstall)
    if (-not (Test-Path $defaultSdkInstall)) {
        New-Item -ItemType Directory -Path $defaultSdkInstall -Force | Out-Null
    }
    try {
        & dotnet build $ProjectPath `
            -t:InstallAndroidDependencies `
            -f net9.0-android `
            -p:AcceptAndroidSDKLicenses=True `
            -p:AndroidSdkDirectory=$defaultSdkInstall
        if ($LASTEXITCODE -ne 0) {
            Stop-WithError ("InstallAndroidDependencies a echoue (code {0}). Voir docs/EMULATEUR-ANDROID.md" -f $LASTEXITCODE)
        }
    }
    catch {
        Stop-WithError ("InstallAndroidDependencies exception: {0}. Voir docs/EMULATEUR-ANDROID.md" -f $_)
    }
    $env:GAIALIFE_ANDROID_SDK = $defaultSdkInstall
    $sdk = Resolve-AndroidSdk
}

if (-not $sdk) {
    Stop-WithError 'Android SDK introuvable. Definissez GAIALIFE_ANDROID_SDK ou ANDROID_HOME. Voir docs/EMULATEUR-ANDROID.md'
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
    Stop-WithError 'JAVA_HOME introuvable (sdkmanager/avdmanager en ont besoin). Installez Microsoft OpenJDK 17+ ou le JDK Android (C:\Program Files\Android\openjdk\...). Voir docs/EMULATEUR-ANDROID.md'
}

$emulator = Resolve-EmulatorExe -SdkRoot $sdk
$avdmanager = Resolve-AvdManager -SdkRoot $sdk

$adb = Get-AdbPath -SdkRoot $sdk
if (-not $adb) {
    Stop-WithError ("adb introuvable. Installez platform-tools dans {0}\platform-tools." -f $sdk)
}
Write-Ok ("adb : {0}" -f $adb)

Initialize-GaiaAvd -Name $AvdName -SdkRoot $sdk -EmulatorExe $emulator -AvdManager $avdmanager

# Apres install sdkmanager, emulator.exe peut venir d'apparaitre
$emulator = Resolve-EmulatorExe -SdkRoot $sdk
if (-not $emulator) {
    Stop-WithError ("emulator.exe introuvable sous {0}\emulator. Installez le package 'emulator' (sdkmanager)." -f $sdk)
}
Write-Ok ("emulator : {0}" -f $emulator)

Start-GaiaAvd -Name $AvdName -EmulatorExe $emulator -Adb $adb

if ($SkipRun) {
    Write-Ok 'SkipRun : emulateur pret, deploiement ignore.'
    exit 0
}

Write-Step 'Build + deploiement App.Mobile (net9.0-android)...'
$msbuildProps = @(
    ("-p:AndroidSdkDirectory={0}" -f $sdk)
)
if ($java) {
    $msbuildProps += ("-p:JavaSdkDirectory={0}" -f $java)
}

Write-Log ("dotnet build -t:Run -f net9.0-android {0}" -f ($msbuildProps -join ' '))
& dotnet build $ProjectPath -t:Run -f net9.0-android @msbuildProps
if ($LASTEXITCODE -ne 0) {
    Stop-WithError ("Build/deploiement en echec (code {0}). Verifiez: dotnet workload install maui. Journal: {1}" -f $LASTEXITCODE, $LogFile)
}

Write-Ok 'App.Mobile deployee et lancee sur l emulateur.'
Write-Host ''
Write-Host 'Pour inspecter le SQLite embarque :' -ForegroundColor Cyan
Write-Host ('  "{0}" shell run-as com.gaialife.mobile ls -la files/' -f $adb)
Write-Host ("Journal : {0}" -f $LogFile)
Write-Log 'Success'
exit 0
