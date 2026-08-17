@echo off
echo ============================================
echo   NHi Inventory Management System - Build
echo ============================================
echo.

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
    pause
    exit /b 1
)

echo [1/3] Restoring packages...
dotnet restore NHi.Inventory\NHi.Inventory.csproj
if %errorlevel% neq 0 (
    echo [ERROR] Package restore failed!
    pause
    exit /b 1
)

echo.
echo [2/3] Building project...
dotnet build NHi.Inventory\NHi.Inventory.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo [3/3] Build successful!
echo.
echo To run the application:
echo   dotnet run --project NHi.Inventory\NHi.Inventory.csproj
echo.
echo Or find the executable in:
echo   NHi.Inventory\bin\Release\net8.0-windows\
echo.
echo Default Login:
echo   Admin: admin / admin
echo   User:  user / user
echo.
pause
