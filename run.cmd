@echo off
setlocal

if not exist "%~dp0BoundaryTick.exe" (
  call "%~dp0build.cmd"
  if errorlevel 1 exit /b %ERRORLEVEL%
)

start "" "%~dp0BoundaryTick.exe"
