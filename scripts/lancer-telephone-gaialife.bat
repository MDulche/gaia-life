@echo off
REM Gaia-Life - double-clic pour deployer App.Mobile sur telephone Android physique
setlocal EnableExtensions
cd /d "%~dp0"

title Gaia-Life - Telephone Android
echo.
echo  Gaia-Life - deploiement telephone Android + App.Mobile
echo.
echo ============================================================
echo  1  USB
echo  2  Wi-Fi
echo  3  USB puis Wi-Fi  (adb tcpip 5555)
echo.
echo  Wi-Fi : IP + Port + Code  (un seul port)
echo  Code vide = deja associe, connect seul
echo ============================================================
echo.

set "MODE="
set /p MODE=Choix [1-3, defaut=2] : 
if "%MODE%"=="" set "MODE=2"

if "%MODE%"=="1" goto :usb
if "%MODE%"=="2" goto :wifi
if "%MODE%"=="3" goto :usb_wifi
echo Choix invalide.
goto :fail_input

:usb
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1"
set ERR=%ERRORLEVEL%
goto :after_deploy

:wifi
set "WIFI_IP="
set "WIFI_PORT="
set "WIFI_CODE="
echo.
set /p WIFI_IP=IP : 
set /p WIFI_PORT=Port : 
set /p WIFI_CODE=Code : 
if "%WIFI_IP%"=="" goto :fail_input
if "%WIFI_PORT%"=="" goto :fail_input
echo.
if "%WIFI_CODE%"=="" (
  echo  adb connect %WIFI_IP%:%WIFI_PORT%
  echo.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiConnectPort "%WIFI_PORT%"
) else (
  echo  adb pair %WIFI_IP%:%WIFI_PORT% ******
  echo  adb connect %WIFI_IP%:%WIFI_PORT%
  echo.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiConnectPort "%WIFI_PORT%" -WifiPairPort "%WIFI_PORT%" -WifiPairCode "%WIFI_CODE%"
)
set ERR=%ERRORLEVEL%
goto :after_deploy

:usb_wifi
set "WIFI_IP="
set /p WIFI_IP=IP Wi-Fi du telephone : 
if "%WIFI_IP%"=="" goto :fail_input
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1" -WifiIp "%WIFI_IP%" -WifiViaUsb
set ERR=%ERRORLEVEL%
goto :after_deploy

:fail_input
echo Saisie incomplete. Voir docs\DEPLOIEMENT-TELEPHONE.md
pause
exit /b 1

:after_deploy
if not %ERR%==0 (
  echo.
  echo ECHEC code %ERR% - docs\DEPLOIEMENT-TELEPHONE.md - scripts\.device-logs\
  powershell.exe -NoProfile -Command "Get-ChildItem '%~dp0.device-logs\*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Get-Content $_.FullName -Tail 20 }"
  echo.
  timeout /t 120 >nul
  pause >nul
  exit /b %ERR%
)

echo.
echo OK. Ctrl+C pour quitter logcat, puis une touche pour fermer.
pause
exit /b 0
