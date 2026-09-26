@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "DEST=%~dp0build"
set "ZIP=%TEMP%\leron-whisper-win-x64.zip"

echo.
echo ================================================================
echo   LERON-AUDIO · DOWNLOAD WHISPER-CLI
echo ================================================================
echo.
echo   Бинарник: whisper-cli.exe (Windows x64)
echo   Папка   : %DEST%
echo.

if exist "%DEST%\whisper-cli.exe" (
    echo   [i] whisper-cli.exe уже есть в build\. Скачивание не требуется.
    pause
    exit /b 0
)

if not exist "%DEST%" mkdir "%DEST%"

set "OK="
for %%U in (
    https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.5/whisper-bin-x64.zip
    https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.5/whisper-blas-bin-x64.zip
    https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.4/whisper-bin-x64.zip
) do if not defined OK (
    echo   [i] Пробую: %%U
    curl.exe -fL --progress-bar -o "%ZIP%" "%%U"
    if not errorlevel 1 set "OK=1"
)

if not defined OK (
    echo.
    echo   [XX] Не удалось скачать ни с одного зеркала. Скачай вручную:
    echo       https://github.com/ggml-org/whisper.cpp/releases
    echo       и распакуй whisper-cli.exe в папку: %DEST%
    pause
    exit /b 1
)

echo   [i] Распаковываю в %DEST% ...
tar.exe -xf "%ZIP%" -C "%DEST%"
if !errorlevel! neq 0 (
    echo   [XX] Распаковка не удалась.
    pause
    exit /b 1
)
del "%ZIP%" >nul 2>&1

:: Fallback: если exe лёг в подпапку архива — поднимаем его в build\
if not exist "%DEST%\whisper-cli.exe" (
    for /r "%DEST%" %%F in (whisper-cli.exe) do copy /y "%%F" "%DEST%\whisper-cli.exe" >nul 2>&1
)

if exist "%DEST%\whisper-cli.exe" (
    echo.
    echo   [OK] whisper-cli.exe готов к работе.
) else (
    echo.
    echo   [XX] whisper-cli.exe не найден в build\ после распаковки.
    echo       Посмотри содержимое архива и положи exe в: %DEST%
)

echo.
pause