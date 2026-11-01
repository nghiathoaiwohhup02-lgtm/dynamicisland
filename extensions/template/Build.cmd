@echo off
rem Double-click to build this toolkit for every Revit version on this PC.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
echo.
pause
