@echo off
rem Ejecuta el ejemplo de WolfGlue con dotnet run (doble clic). Cuando exista htaW, lanzara htaW.
cd /d "%~dp0"
dotnet run --project samples\Hola\Hola.csproj -- %*
if errorlevel 1 pause
