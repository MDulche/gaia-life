@echo off
REM Gaia-Life - double-clic pour deployer App.Mobile sur telephone Android physique
setlocal EnableExtensions
cd /d "%~dp0"

title Gaia-Life - Telephone Android
echo.
echo  Gaia-Life - deploiement telephone Android + App.Mobile
echo  (cable USB ou adb Wi-Fi deja connecte ; ne fermez pas cette fenetre)
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-device.ps1"
set ERR=%ERRORLEVEL%

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
