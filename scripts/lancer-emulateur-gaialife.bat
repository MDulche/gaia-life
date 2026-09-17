@echo off
REM Gaia-Life — double-clic pour démarrer l'émulateur + déployer App.Mobile
setlocal
cd /d "%~dp0"

echo.
echo  Gaia-Life - lancement emulateur Android + App.Mobile
echo  (fenetre PowerShell ; fermez-la seulement apres le deploiement)
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-emulator.ps1"
set ERR=%ERRORLEVEL%

if not %ERR%==0 (
  echo.
  echo ECHEC (code %ERR%). Voir docs\EMULATEUR-ANDROID.md et scripts\.emulator-logs\
  pause
  exit /b %ERR%
)

echo.
echo Termine. L'emulateur reste ouvert ; vous pouvez fermer cette fenetre.
pause
exit /b 0
