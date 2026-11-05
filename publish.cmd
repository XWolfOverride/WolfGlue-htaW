@echo off
rem Publica htaw.exe (NativeAOT) y lo deja en la raiz del proyecto.
rem Solo compila: no comprueba requisitos ni descarga nada (para eso, build.ps1).
rem Se puede lanzar con doble clic.
setlocal
cd /d "%~dp0"
set "ERR=0"

set "ARCH=%PROCESSOR_ARCHITEW6432%"
if not defined ARCH set "ARCH=%PROCESSOR_ARCHITECTURE%"
set "RID="
if /i "%ARCH%"=="AMD64" set "RID=win-x64"
if /i "%ARCH%"=="ARM64" set "RID=win-arm64"
if not defined RID (
    echo [X]  Arquitectura no soportada: %ARCH%
    set "ERR=1"
    goto :fin
)

rem ILCompiler invoca "vswhere.exe" por nombre: debe estar en el PATH.
set "PATH=%PATH%;%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"
set "DOTNET_NOLOGO=1"

if exist publish rmdir /s /q publish
echo ^>^>   Compilando htaw (NativeAOT, %RID%)... puede tardar un par de minutos
dotnet publish src\htaW\htaW.csproj -c Release -r %RID% -o publish\htaw -nologo -v q
if errorlevel 1 goto :error
copy /y publish\htaw\htaw.exe htaw.exe >nul
if errorlevel 1 goto :error
rmdir /s /q publish
echo [OK] htaw.exe -^> %~dp0htaw.exe
goto :fin

:error
echo [X]  Error al publicar.
set "ERR=1"

:fin
rem Pausa solo si se abrio con doble clic (no desde una consola ya abierta).
echo %cmdcmdline% | find /i "%~0" >nul && pause
exit /b %ERR%
