@echo off
title Revit MCP Release Builder
cd /d "%~dp0"
echo ====================================================
echo          Revit MCP - Local Release Builder
echo ====================================================
echo.

echo [1/3] Building Node.js MCP Server...
cd server
call npm install
call npm run build
cd ..
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed to build Node.js MCP Server.
    pause
    exit /b %ERRORLEVEL%
)
echo [SUCCESS] Node.js MCP Server built.
echo.

echo [2/3] Building Revit C# plugin (Debug & Release R27)...
rem Using portable .NET 10 SDK from the parent directory
set "PORTABLE_DOTNET=..\.dotnet\dotnet.exe"
if exist "%PORTABLE_DOTNET%" (
    echo Using portable .NET SDK: %PORTABLE_DOTNET%
    "%PORTABLE_DOTNET%" build mcp-servers-for-revit.sln -c "Debug R27"
    "%PORTABLE_DOTNET%" build mcp-servers-for-revit.sln -c "Release R27"
) else (
    echo Portable .NET SDK not found. Trying system 'dotnet'...
    dotnet build mcp-servers-for-revit.sln -c "Debug R27"
    dotnet build mcp-servers-for-revit.sln -c "Release R27"
)
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed to compile C# solution.
    pause
    exit /b %ERRORLEVEL%
)
echo [SUCCESS] C# solution compiled successfully.
echo.

echo [3/3] Compiling install.py into standalone install.exe...
pyinstaller --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo PyInstaller not found. Installing via pip...
    pip install pyinstaller
)

call pyinstaller --onefile --console --name install install.py
if %ERRORLEVEL% neq 0 (
    echo [ERROR] PyInstaller compilation failed.
    pause
    exit /b %ERRORLEVEL%
)

echo Cleaning up PyInstaller temporary files...
move /y dist\install.exe . >nul
rmdir /s /q dist >nul 2>&1
rmdir /s /q build >nul 2>&1
del /f /q install.spec >nul 2>&1

echo [SUCCESS] install.exe compiled and placed in root folder.
echo.
echo ====================================================
echo            BUILD PROCESS COMPLETED SUCCESSFULLY!
echo ====================================================
pause
