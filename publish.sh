#!/usr/bin/env bash
# TTKManager portable publish script — single-file self-contained Windows x64 build.
# Output: publish/TTKManager.exe (no install, no .NET runtime required on target).

set -euo pipefail
cd "$(dirname "$0")"

export MSBuildSDKsPath="C:/Program Files/dotnet/sdk/9.0.313/Sdks"

dotnet publish src/TTKManager.App/TTKManager.App.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -o publish

echo
echo "==============================================="
echo " Portable build ready in: $(pwd)/publish"
echo " Run: publish/TTKManager.exe"
echo "==============================================="
