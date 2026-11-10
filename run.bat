@echo off
cd /d "%~dp0"
if not exist "dist\TransparentWindow.exe" (
  call build.bat
  if errorlevel 1 exit /b 1
)
start "" "dist\TransparentWindow.exe"
