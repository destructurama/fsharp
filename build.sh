#!/bin/bash

set -e 

export PATH=$HOME/.dotnetcli:$PATH

dotnet --info
dotnet --list-sdks
dotnet restore

echo "🤖 Attempting to build..."
for path in src/**/*.fsproj; do
    dotnet build -c Release ${path}
done

echo "🤖 Running tests..."
for path in test/*.Tests/*.fsproj; do
    dotnet test -c Release ${path}
done
