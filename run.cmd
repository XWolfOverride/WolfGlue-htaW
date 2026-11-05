@echo off
rem Ejecuta htaW con dotnet run (doble clic). Sin argumentos pide el .htaw a abrir.
cd /d "%~dp0"
dotnet run --project src\htaW\htaW.csproj -- %*
if errorlevel 1 pause
