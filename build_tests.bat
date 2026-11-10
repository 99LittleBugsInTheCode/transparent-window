@echo off
setlocal
cd /d "%~dp0"

call build.bat
if errorlevel 1 exit /b 1

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "dist\test" mkdir "dist\test"

echo Building test tools ...
"%CSC%" /nologo /target:winexe /platform:anycpu /codepage:65001 ^
  /out:"dist\test\TestWindow.exe" ^
  /reference:System.dll,System.Drawing.dll,System.Windows.Forms.dll ^
  test\TestWindow.cs
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:exe /platform:anycpu /codepage:65001 ^
  /out:"dist\test\Probe.exe" /reference:System.dll ^
  src\Native.cs src\OpacityService.cs test\Probe.cs
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:exe /platform:anycpu /codepage:65001 ^
  /out:"dist\test\UiE2E.exe" ^
  /reference:System.dll,System.Drawing.dll src\Native.cs src\OpacityService.cs test\UiE2E.cs
if errorlevel 1 exit /b 1

echo [OK] dist\test\TestWindow.exe
echo [OK] dist\test\Probe.exe
echo [OK] dist\test\UiE2E.exe
endlocal
