@echo off
setlocal

set "PROJECT_DIR=%~dp0WeightVerificationQR"
set "APP_EXE=%PROJECT_DIR%\src\WeightVerificationQR.App\bin\Debug\net8.0-windows\WeightVerificationQR.exe"
set "PROJECT_FILE=%PROJECT_DIR%\src\WeightVerificationQR.App\WeightVerificationQR.App.csproj"

echo Building latest Weight Verification QR...
dotnet build "%PROJECT_FILE%"
if errorlevel 1 (
    echo.
    echo Build failed. Check the error shown above.
    pause
    exit /b 1
)

start "" /D "%PROJECT_DIR%\src\WeightVerificationQR.App\bin\Debug\net8.0-windows" "%APP_EXE%"
endlocal
