@echo off
setlocal
if exist "%~dp0WordFloatSetup.exe" (
  start "" "%~dp0WordFloatSetup.exe"
  exit /b 0
)
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0WordFloat.ps1" -Mode Install
if errorlevel 1 (
  echo WordFloat failed. Please keep the error message above.
  pause
)
