@echo off
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo csc.exe was not found.
  exit /b 1
)

"%CSC%" ^
  /nologo ^
  /target:winexe ^
  /platform:anycpu ^
  /optimize+ ^
  /out:"%~dp0BoundaryTick.exe" ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "%~dp0BoundaryTick.cs"

exit /b %ERRORLEVEL%
