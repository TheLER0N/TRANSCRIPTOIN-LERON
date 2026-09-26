@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
cd /d "%~dp0"
set "ROOT=%~dp0"
set "PROJ=%ROOT%code\Leron.Audio.csproj"
echo.
echo ================================================================
echo LERON-AUDIO · LOCAL RUN
echo ================================================================
echo.
where dotnet >nul 2>&1
if !errorlevel! neq 0 (
echo [XX] dotnet не найден. Установи .NET 8 SDK.
pause
exit /b 1
)
if not exist "%PROJ%" (
echo [XX] Проект не найден: %PROJ%
pause
exit /b 1
)
echo [i] Восстановление пакетов и сборка...
dotnet build "%PROJ%" -c Debug --nologo -v q
if !errorlevel! neq 0 (
echo [XX] Сборка не удалась. Смотри ошибки выше.
pause
exit /b 1
)
echo [i] Запуск LERON-AUDIO...
dotnet run --project "%PROJ%" --no-build
endlocal