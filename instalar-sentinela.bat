@echo off
chcp 65001 >nul
title Instalar Sentinela (admin)

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  [ERRO] Rode este arquivo como ADMINISTRADOR.
    echo         Clique com o botao direito ^> Executar como administrador.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0instalar-sentinela.ps1" %*
pause
