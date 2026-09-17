@echo off
REM Gaia-Life - double-clic pour deployer App.Mobile sur telephone Android physique
setlocal EnableExtensions
cd /d "%~dp0"

title Gaia-Life - Telephone Android
echo.
echo  Gaia-Life - deploiement telephone Android + App.Mobile
echo  (ne fermez pas cette fenetre)
echo.
echo ============================================================
echo  Connexion ADB Wi-Fi (Android 11+)
echo  Sur le telephone : Options developpeurs ^> Debogage sans fil
echo    1) Associer un appareil avec un code  -^> IP, port pairing, code
echo    2) Ecran principal Debogage sans fil  -^> port de connexion
echo  Laissez l'IP vide pour USB / session adb deja connectee.
echo ============================================================
echo.

set "WIFI_IP="
set "WIFI_PAIR_PORT="
set "WIFI_PAIR_CODE="
set "WIFI_CONN_PORT="

set /p WIFI_IP=IP du telephone (ex. 192.168.1.42, vide=USB) : 
if "%WIFI_IP%"=="" goto :deploy

set /p WIFI_PAIR_PORT=Port de pairing : 
set /p WIFI_PAIR_CODE=Code d'association (6 chiffres) : 
set /p WIFI_CONN_PORT=Port de connexion : 

if "%WIFI_PAIR_PORT%"=="" (
  echo ERREUR : port de pairing requis.
  goto :fail_input
)
if "%WIFI_PAIR_CODE%"=="" (
  echo ERREUR : code d'association requis.
  goto :fail_input
)
if "%WIFI_CONN_PORT%"=="" (
  echo ERREUR : port de connexion requis.
  goto :fail_input
)

echo.
echo  Pairing %WIFI_IP%:%WIFI_PAIR_PORT% puis connect %WIFI_IP%:%WIFI_CONN_PORT% ...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiPairPort "%WIFI_PAIR_PORT%" -WifiPairCode "%WIFI_PAIR_CODE%" -WifiConnectPort "%WIFI_CONN_PORT%"
set ERR=%ERRORLEVEL%
goto :after_deploy

:deploy
echo  Mode USB / deja connecte - pas de pairing Wi-Fi.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1"
set ERR=%ERRORLEVEL%
goto :after_deploy

:fail_input
echo.
echo Guide : docs\DEPLOIEMENT-TELEPHONE.md
echo Appuyez sur une touche pour fermer...
pause >nul
exit /b 1

:after_deploy
if not %ERR%==0 (
  echo.
  echo ============================================================
  echo  ECHEC (code %ERR%)
  echo  Guide : docs\DEPLOIEMENT-TELEPHONE.md
  echo  Logs  : scripts\.device-logs\
  echo ============================================================
  echo.
  echo --- Dernieres lignes du journal ---
  powershell.exe -NoProfile -Command "Get-ChildItem '%~dp0.device-logs\*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Write-Host $_.FullName; Get-Content $_.FullName -Tail 25 }"
  echo.
  echo Appuyez sur une touche pour fermer (ou attente 120s)...
  timeout /t 120 >nul
  pause >nul
  exit /b %ERR%
)

echo.
echo Deploiement OK. Les logs logcat s'affichent ci-dessus (Ctrl+C dans PowerShell pour quitter).
echo Appuyez sur une touche pour fermer...
pause
exit /b 0
