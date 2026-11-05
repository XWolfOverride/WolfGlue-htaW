#!/usr/bin/env sh
# Ejecuta el ejemplo de WolfGlue con dotnet run. Cuando exista htaW, lanzara htaW.
cd "$(dirname "$0")" || exit 1
exec dotnet run --project samples/Hola/Hola.csproj -- "$@"
