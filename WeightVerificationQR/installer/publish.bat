@echo off
REM Publishes a self-contained, single-file Release build of the app
REM into ..\publish\WeightVerificationQR, ready to be picked up by
REM WeightVerificationQR.iss (Inno Setup) to produce an installer.
REM
REM Run this from the installer\ folder on a Windows machine with the
REM .NET 8 SDK installed.

setlocal
set PROJECT=..\src\WeightVerificationQR.App\WeightVerificationQR.App.csproj
set OUTDIR=..\publish\WeightVerificationQR

echo Publishing %PROJECT% to %OUTDIR% ...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUTDIR%"

if %ERRORLEVEL% NEQ 0 (
    echo Publish failed.
    exit /b %ERRORLEVEL%
)

echo.
echo Done. Open WeightVerificationQR.iss in Inno Setup and compile it,
echo or run: ISCC.exe WeightVerificationQR.iss
endlocal
