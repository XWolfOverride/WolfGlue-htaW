#!/usr/bin/env sh
# Ejecuta htaW con dotnet run. Sin argumentos pide el .htaw a abrir.
cd "$(dirname "$0")" || exit 1
exec dotnet run --project src/htaW/htaW.csproj -- "$@"
