@echo off
chcp 65001 > nul
title AlpFinans Local MCP Server
echo ========================================================
echo  AlpFinans Model Context Protocol (MCP) Server
echo  Baglanti Modu: Stdio / HTTP & SSE
echo ========================================================
echo.

set "PYEXE=C:\Users\Alphan.MonsterAlphan\AppData\Local\Programs\Python\Python312\python.exe"
if not exist "%PYEXE%" (
    set "PYEXE=python"
)

if "%1"=="" (
    echo [1] Stdio Modu (Claude Desktop, Cursor, Antigravity)
    echo [2] HTTP & SSE Modu (Google Spark, Web Ajanlari, Browser - Port 8765)
    echo.
    set /p choice="Lutfen calisma modunu secin (1 veya 2, varsayilan 2): "
    if "%choice%"=="1" (
        goto run_stdio
    ) else (
        goto run_http
    )
)

if "%1"=="--stdio" goto run_stdio
if "%1"=="--http" goto run_http

:run_http
echo.
echo [HTTP & SSE Sunucusu Baslatiliyor...]
echo Web Dokumantasyonu: http://127.0.0.1:8765/
echo SSE Ucu (Spark/Web): http://127.0.0.1:8765/sse
echo MCP JSON-RPC Ucu:   http://127.0.0.1:8765/mcp
echo.
"%PYEXE%" "%~dp01_kisisel_finans_ve_vergi\mcp_server.py" --http --port 8765
pause
exit /b

:run_stdio
echo.
echo [Stdio JSON-RPC Modunda Calisiyor...]
"%PYEXE%" "%~dp01_kisisel_finans_ve_vergi\mcp_server.py" --stdio
pause
exit /b
