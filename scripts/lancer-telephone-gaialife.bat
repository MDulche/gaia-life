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
echo  Choix de connexion
echo.
echo   1  USB  (cable branche, debogage USB accepte)  [recommande]
echo   2  Wi-Fi deja associe  (IP + port ecran principal)
echo   3  Wi-Fi premiere association  (pair + connect)
echo   4  USB puis Wi-Fi  (cable une fois : adb tcpip 5555)
echo.
echo  Astuce : le port Wi-Fi CHANGE souvent. Recopiez-le juste
echo  avant de lancer. Erreur 10061 = port refuse / mauvais port /
echo  reseau isole (invite) / Debogage sans fil desactive.
echo ============================================================
echo.

set "MODE="
set /p MODE=Votre choix [1-4, defaut=1] : 
if "%MODE%"=="" set "MODE=1"

if "%MODE%"=="1" goto :usb
if "%MODE%"=="2" goto :wifi_known
if "%MODE%"=="3" goto :wifi_pair
if "%MODE%"=="4" goto :usb_wifi
echo Choix invalide.
goto :fail_input

:usb
echo.
echo  Mode USB...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1"
set ERR=%ERRORLEVEL%
goto :after_deploy

:wifi_known
set "WIFI_IP="
set "WIFI_CONN_PORT="
set /p WIFI_IP=IP (ex. 172.16.101.247) : 
set /p WIFI_CONN_PORT=Port connexion (haut de Debogage sans fil) : 
if "%WIFI_IP%"=="" goto :fail_input
if "%WIFI_CONN_PORT%"=="" goto :fail_input
echo.
echo  adb connect %WIFI_IP%:%WIFI_CONN_PORT% ...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiConnectPort "%WIFI_CONN_PORT%"
set ERR=%ERRORLEVEL%
goto :after_deploy

:wifi_pair
set "WIFI_IP="
set "WIFI_PAIR_PORT="
set "WIFI_PAIR_CODE="
echo.
echo  Premiere association :
echo   - Ouvrez "Associer un appareil avec un code" -^> port PAIRING + code
echo   - Apres le pairing OK, le script vous redemandera le port CONNEXION
echo     actuel (ecran principal Debogage sans fil : il CHANGE souvent).
echo.
set /p WIFI_IP=IP : 
set /p WIFI_PAIR_PORT=Port PAIRING (fenetre association) : 
set /p WIFI_PAIR_CODE=Code (6 chiffres) : 
if "%WIFI_IP%"=="" goto :fail_input
if "%WIFI_PAIR_PORT%"=="" goto :fail_input
if "%WIFI_PAIR_CODE%"=="" goto :fail_input
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiPairPort "%WIFI_PAIR_PORT%" -WifiPairCode "%WIFI_PAIR_CODE%"
set ERR=%ERRORLEVEL%
goto :after_deploy

:usb_wifi
set "WIFI_IP="
set /p WIFI_IP=IP du telephone (Wi-Fi, ex. 172.16.101.247) : 
if "%WIFI_IP%"=="" goto :fail_input
echo.
echo  Cable USB requis maintenant. Puis bascule vers %WIFI_IP%:5555
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiViaUsb
set ERR=%ERRORLEVEL%
goto :after_deploy

:fail_input
echo.
echo ERREUR : saisie incomplete.
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
