@echo off
setlocal
echo ============================================
echo   NHi Inventory Management System - Build
echo ============================================
echo.

:: This script lives in \scripts\, so step up to the repository root and
:: address the project by its organized location under \src\.
pushd "%~dp0.."
set "CSPROJ=src\NHi.Inventory\NHi.Inventory.csproj"

:: Check if .NET SDK is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET 8 SDK is not installed!
    echo.
    echo Please install .NET 8 SDK from:
    https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Select: .NET Desktop Runtime 8.0.x (for Windows)
    echo Then install the SDK from the same page.
    echo.
    popd
    pause
    exit /b 1
)

echo [1/3] Restoring packages...
dotnet restore "%CSPROJ%"
if %errorlevel% neq 0 (
    echo [ERROR] Package restore failed!
    popd
    pause
    exit /b 1
)

echo.
echo [2/3] Building project...
dotnet build "%CSPROJ%" -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Build failed!
    popd
    pause
    exit /b 1
)

echo.
echo [3/3] Build successful!
echo.
echo To run the application:
echo   dotnet run --project src\NHi.Inventory\NHi.Inventory.csproj
echo.
echo Or find the executable in:
echo   src\NHi.Inventory\bin\Release\net8.0-windows\
echo.
echo Default Login:
echo   Shop user: user / user
echo   Administrator password is set on first run (not stored in the repo).
echo.
popd
pause
