@echo off
REM Gaia-Life - double-clic pour demarrer l'emulateur + deployer App.Mobile
setlocal EnableExtensions
cd /d "%~dp0"

title Gaia-Life - Emulateur Android
echo.
echo  Gaia-Life - lancement emulateur Android + App.Mobile
echo  (ne fermez pas cette fenetre avant la fin)
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-emulator.ps1"
set ERR=%ERRORLEVEL%

if not %ERR%==0 (
  echo.
  echo ============================================================
  echo  ECHEC (code %ERR%)
  echo  Guide : docs\EMULATEUR-ANDROID.md
  echo  Logs  : scripts\.emulator-logs\
  echo ============================================================
  echo.
  echo --- Dernieres lignes du journal ---
  powershell.exe -NoProfile -Command "Get-ChildItem '%~dp0.emulator-logs\*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Write-Host $_.FullName; Get-Content $_.FullName -Tail 25 }"
  echo.
  echo Appuyez sur une touche pour fermer (ou attente 120s)...
  timeout /t 120 >nul
  pause >nul
  exit /b %ERR%
)

echo.
echo Termine. L'emulateur reste ouvert.
echo Appuyez sur une touche pour fermer...
pause
exit /b 0
