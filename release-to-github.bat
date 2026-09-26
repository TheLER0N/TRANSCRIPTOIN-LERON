@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
set "ROOT=%~dp0"
set "PROJ=%ROOT%code\Leron.Audio.csproj"
set "BUILD_DIR=%ROOT%release-build"
set "MODEL=%ROOT%ggml-large-v3-turbo.bin"

echo.
echo ================================================================
echo   LERON-AUDIO · RELEASE TO GITHUB
echo ================================================================
echo.
echo   Репозиторий: https://github.com/TheLER0N/TRANSCRIPTOIN-LERON
echo.

:: ── Проверка окружения ──────────────────────────────────────────
where dotnet >nul 2>&1
if !errorlevel! neq 0 (
    echo   [XX] dotnet не найден. Установи .NET 8 SDK.
    pause
    exit /b 1
)

if not exist "%PROJ%" (
    echo   [XX] Leron.Audio.csproj не найден: %PROJ%
    pause
    exit /b 1
)

if not exist "%MODEL%" (
    echo   [XX] Модель Whisper не найдена: %MODEL%
    echo       Скачай ggml-large-v3-turbo.bin и положи в корень проекта.
    pause
    exit /b 1
)

:: ── Ввод данных релиза ──────────────────────────────────────────
set /p "VER=   Версия (например 1.0.0): "
if "!VER!"=="" (
    echo   [XX] Версия не указана.
    pause
    exit /b 1
)

set /p "TITLE=   Название (Enter = LERON-AUDIO v!VER!): "
if "!TITLE!"=="" set "TITLE=LERON-AUDIO v!VER!"

set /p "DESC=    Описание (Enter = дефолт): "
if "!DESC!"=="" set "DESC=LERON-AUDIO v!VER! — локальная PTT-диктовка через Whisper.cpp."

echo.
echo ================================================================
echo   Версия   : !VER!
echo   Название : !TITLE!
echo   Проект   : %PROJ%
echo   Сборка   : %BUILD_DIR%
echo ================================================================
echo.

set /p "CONFIRM=   Продолжить? [Y/n]: "
if /i "!CONFIRM!"=="n" (
    echo   Отменено.
    pause
    exit /b 0
)

:: ── [1/4] Сборка Release ────────────────────────────────────────
echo.
echo   [1/4] dotnet publish (Release)...

if exist "%BUILD_DIR%" (
    :: Не удаляем всю папку — бережём settings.json, если пользователь там что-то положил
    for %%F in ("%BUILD_DIR%\*.exe" "%BUILD_DIR%\*.dll" "%BUILD_DIR%\*.bin") do (
        if /i not "%%~nxF"=="settings.json" del "%%F" 2>nul
    )
    if exist "%BUILD_DIR%\runtimes" rd /s /q "%BUILD_DIR%\runtimes" 2>nul
)

dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained false -o "%BUILD_DIR%" --nologo -v q
if !errorlevel! neq 0 (
    echo.
    echo   [XX] Сборка не удалась — повтори dotnet publish вручную.
    pause
    exit /b 1
)
echo   [OK] Собрано в %BUILD_DIR%

:: ── [2/4] Копирование модели Whisper ────────────────────────────
echo.
echo   [2/4] Копирую модель Whisper в релизную папку...
copy /y "%MODEL%" "%BUILD_DIR%\ggml-large-v3-turbo.bin" >nul
if !errorlevel! neq 0 (
    echo   [XX] Не удалось скопировать модель.
    pause
    exit /b 1
)
for %%A in ("%MODEL%") do set /a "MODEL_MB=%%~zA / 1048576"
echo   [OK] Модель скопирована (~!MODEL_MB! MB)

:: Если есть whisper-cli.exe в build/, тоже копируем
if exist "%ROOT%build\whisper-cli.exe" (
    copy /y "%ROOT%build\whisper-cli.exe" "%BUILD_DIR%\whisper-cli.exe" >nul
    echo   [OK] whisper-cli.exe скопирован
)

:: ── [3/4] Создание ZIP ──────────────────────────────────────────
echo.
echo   [3/4] Создание архива...
set "ZIP_NAME=LERON-AUDIO-!VER!-win-x64.zip"
set "ZIP_PATH=%ROOT%release\!ZIP_NAME!"
if not exist "%ROOT%release" mkdir "%ROOT%release"
if exist "!ZIP_PATH!" del "!ZIP_PATH!"

powershell -NoProfile -Command "Compress-Archive -Path '%BUILD_DIR%\*' -DestinationPath '!ZIP_PATH!' -Force"
if !errorlevel! neq 0 (
    echo   [XX] Не удалось создать архив.
    pause
    exit /b 1
)

for %%A in ("!ZIP_PATH!") do set /a "ZIP_MB=%%~zA / 1048576"
echo   [OK] Архив: !ZIP_PATH! (~!ZIP_MB! MB)

:: ── [4/4] Открытие страницы релиза ──────────────────────────────
echo.
echo   [4/4] Открываю GitHub Releases...
echo.
echo ================================================================
echo   ГОТОВО!
echo ================================================================
echo.
echo   Что сделать в браузере:
echo.
echo   1. В поле "Tag version" введи:   !VER!
echo   2. В поле "Release title" введи: !TITLE!
echo   3. В поле "Describe" вставь:     !DESC!
echo   4. Нажми "attaching binaries"
echo   5. Выбери файл: !ZIP_PATH!
echo   6. Нажми "Publish release"
echo.
echo   Путь к архиву скопирован в буфер обмена —
echo   в поле выбора файла нажми Ctrl+V и Enter.
echo.
:: Копируем путь в буфер
echo|set /p="!ZIP_PATH!" | clip
start "" "https://github.com/TheLER0N/TRANSCRIPTOIN-LERON/releases/new?tag=!VER!&title=!TITLE!"
pause