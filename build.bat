@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] .NET Framework 4.x C# compiler not found.
  exit /b 1
)

if not exist "assets\icon.ico" (
  echo [ERROR] assets\icon.ico is missing. Run: python tools\make_icon.py
  exit /b 1
)
if not exist "dist" mkdir "dist"

echo Building dist\TransparentWindow.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /out:"dist\TransparentWindow.exe" /win32icon:"assets\icon.ico" ^
  /reference:System.dll,System.Drawing.dll,System.Windows.Forms.dll ^
  src\*.cs
if errorlevel 1 (
  echo [ERROR] Build failed.
  exit /b 1
)

echo [OK] dist\TransparentWindow.exe
endlocal
