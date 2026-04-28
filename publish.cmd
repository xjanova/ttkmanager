@echo off
REM TTKManager portable publish script — single-file self-contained Windows x64 build.
REM Output: publish/TTKManager.App.exe (no install, no .NET runtime required on target).

setlocal
pushd "%~dp0"

set MSBuildSDKsPath=C:\Program Files\dotnet\sdk\9.0.313\Sdks

dotnet publish src\TTKManager.App\TTKManager.App.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=embedded ^
    -p:EnableCompressionInSingleFile=true ^
    -o publish

if errorlevel 1 (
    echo.
    echo Publish failed.
    popd
    exit /b 1
)

echo.
echo ===============================================
echo  Portable build ready in: %CD%\publish
echo  Run: publish\TTKManager.App.exe
echo ===============================================

popd
endlocal
