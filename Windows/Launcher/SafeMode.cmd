@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0WordFloat.ps1" -Mode SafeLaunch
if errorlevel 1 (
  echo WordFloat failed. Please keep the error message above.
  pause
)
