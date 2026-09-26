@echo off
chcp 65001 >nul 2>&1
setlocal
cd /d "%~dp0"
set "STARTPATH=%~dp0"
echo.
echo ================================================================
echo   NEW ERA · PROJECT REPORT
echo ================================================================
echo.
echo   Батник открыт из : %STARTPATH%
echo   Сейчас откроется окно выбора папки для отчёта.
echo.
:: ── Выбор папки (диалог стартует из папки батника) ──────────
set "TARGET="
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "Add-Type -AssemblyName System.Windows.Forms;$d=New-Object System.Windows.Forms.FolderBrowserDialog;$d.Description='Выбери папку для отчёта';$d.SelectedPath=$env:STARTPATH;$d.ShowNewFolderButton=$false;if($d.ShowDialog()-eq 'OK'){$d.SelectedPath}"`) do set "TARGET=%%P"
:: ── Если диалог закрыли — ввод пути вручную ─────────────────
if not defined TARGET (
echo   [i] Окно закрыто. Укажи путь вручную.
echo       Папка батника: %STARTPATH%
set /p "TARGET=  Путь к папке для отчёта ^(Enter = папка батника^)> "
)
if not defined TARGET set "TARGET=%STARTPATH%"
:: убрать завершающий обратный слэш
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"
set "ROOT=%TARGET%"
if not exist "%ROOT%\" (
echo   [XX] Папка не найдена: %ROOT%
pause
exit /b 1
)
for %%I in ("%ROOT%") do set "NAME=%%~nxI"
echo.
echo ================================================================
echo   FOLDER : %NAME%
echo   PATH   : %ROOT%
echo ================================================================
echo.
echo   [i] Формирую отчёт (авто-разбиение при превышении 18 МБ)...
echo.
:: ─── Пути для PowerShell ────────────────────────────────────
set "NR_ROOT=%ROOT%"
set "NR_NAME=%NAME%"
:: ─── Отчёт с авто-разбиением (UTF-8 с BOM) ─────────────────
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
 "$r=$env:NR_ROOT;$nm=$env:NR_NAME;" ^
 "[Console]::OutputEncoding=[Text.Encoding]::UTF8;" ^
 "$m='.cs','.bat','.cmd','.ps1','.json','.xml','.xaml','.csproj','.sln','.py','.md','.sh','.txt','.gitignore','.js','.ts','.html','.css','.yml','.yaml','.toml','.ini','.env','.cfg';" ^
 "$skip=@('bin','obj','.git','.vs','.vscode','.idea');" ^
 "$skipRe='\\(bin|obj|\.git|\.vscode|\.vs|\.idea)\\';" ^
 "$maxLen=9500000;" ^
 "function T($p,$pf){$e=Get-ChildItem -LiteralPath $p|Where-Object {($skip -notcontains $_.Name)}|Sort-Object Name;$cnt=@($e).Count;for($i=0;$i-lt$cnt;$i++){$it=$e[$i];$l=$i-eq($cnt-1);if($l){$cn='\---'}else{$cn='+---'};$pf+$cn+$it.Name;if($it.PSIsContainer){if($l){$nx=$pf+'    '}else{$nx=$pf+'|   '};T $it.FullName $nx}}};" ^
 "$hdr=$nm+'_report';" ^
 "$L='='*64;" ^
 "$header=[Text.StringBuilder]::new();" ^
 "[void]$header.AppendLine($L);" ^
 "[void]$header.AppendLine('  PROJECT REPORT : '+$nm);" ^
 "[void]$header.AppendLine('  GENERATED      : '+(Get-Date -Format 'dd.MM.yyyy HH:mm:ss'));" ^
 "[void]$header.AppendLine('  PATH           : '+$r);" ^
 "[void]$header.AppendLine($L);" ^
 "[void]$header.AppendLine('');" ^
 "[void]$header.AppendLine('==================== STRUCTURE ====================');" ^
 "[void]$header.AppendLine('');" ^
 "[void]$header.AppendLine($r);" ^
 "foreach($ln in @(T $r '')){[void]$header.AppendLine($ln)};" ^
 "[void]$header.AppendLine('');" ^
 "[void]$header.AppendLine('==================== SOURCE FILES ====================');" ^
 "$fs=Get-ChildItem -LiteralPath $r -Recurse -File|Where-Object {($m -contains $_.Extension.ToLower()) -and ($_.FullName -notmatch $skipRe) -and ($_.Name -notlike ($nm+'_report*'))};" ^
 "$fc=0;$partNum=1;$sb=[Text.StringBuilder]::new();" ^
 "[void]$sb.Append($header.ToString());" ^
 "$outFiles=@();" ^
 "function WritePart{$script:o=Join-Path $r ($hdr+'_part'+$partNum+'.txt');if($partNum-eq 1 -and $fs.Count-lt 200){$script:o=Join-Path $r ($hdr+'.txt')};[IO.File]::WriteAllText($script:o,$sb.ToString(),[Text.UTF8Encoding]::new($true));$script:outFiles+=$script:o;Write-Host ('  [OK] Часть '+$partNum+': '+$script:o+' ('+[math]::Round((Get-Item $script:o).Length/1MB,1)+' MB)');};" ^
 "foreach($f in $fs){$fc++;$rel=$f.FullName.Substring($r.Length+1);" ^
 "$entry=''+[Environment]::NewLine+'-'*48+[Environment]::NewLine+'FILE: '+$rel+[Environment]::NewLine+'SIZE: '+$f.Length+' bytes'+[Environment]::NewLine+'-'*48+[Environment]::NewLine;" ^
 "try{$txt=[IO.File]::ReadAllText($f.FullName,[Text.Encoding]::UTF8);$entry+=$txt.TrimEnd()}catch{$entry+='  [!!] Ошибка чтения: '+$_.Exception.Message};" ^
 "$entry+=[Environment]::NewLine;" ^
 "if(($sb.Length+$entry.Length)-gt$maxLen){WritePart;$partNum++;$sb=[Text.StringBuilder]::new();[void]$sb.AppendLine($L);[void]$sb.AppendLine('  PROJECT REPORT : '+$nm+' (часть '+$partNum+')');[void]$sb.AppendLine('  PATH           : '+$r);[void]$sb.AppendLine($L);[void]$sb.AppendLine('')};" ^
 "[void]$sb.Append($entry);Write-Host ('  [OK] '+$rel)};" ^
 "if($sb.Length-gt 0){WritePart};" ^
 "[void]$sb.Clear();[void]$sb.AppendLine($L);[void]$sb.AppendLine('  Source files : '+$fc);[void]$sb.AppendLine('  Parts        : '+$partNum);[void]$sb.AppendLine($L);Write-Host '';Write-Host ('  [OK] Обработано файлов: '+$fc);Write-Host ('  [OK] Частей отчёта   : '+$partNum);if($partNum-gt 1){Write-Host '';Write-Host '  Файлы:';foreach($of in $outFiles){Write-Host ('    '+$of)}}"
:: ── Сохраняем список файлов для открытия ────────────────────
set "OUTLIST=%ROOT%\%NAME%_report_files.tmp"
powershell -NoProfile -Command "Get-ChildItem -LiteralPath '%ROOT%' -Filter '%NAME%_report*' | Where-Object {$_.Extension -eq '.txt'} | Sort-Object Name | ForEach-Object {$_.FullName} | Out-File -LiteralPath '%OUTLIST%' -Encoding UTF8"
:: ─── Поиск Notepad++ ────────────────────────────────────────
set "NPP="
where notepad++ >nul 2>&1 && set "NPP=notepad++"
if not defined NPP for /f "tokens=2*" %%A in ('reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe" /ve 2^>nul') do set "NPP=%%B"
if not defined NPP for /f "tokens=2*" %%A in ('reg query "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe" /ve 2^>nul') do set "NPP=%%B"
if not defined NPP if exist "%ProgramFiles%\Notepad++\notepad++.exe" set "NPP=%ProgramFiles%\Notepad++\notepad++.exe"
set "PF86=%ProgramFiles(x86)%"
if not defined NPP if exist "%PF86%\Notepad++\notepad++.exe" set "NPP=%PF86%\Notepad++\notepad++.exe"
if not defined NPP if exist "%LOCALAPPDATA%\Programs\Notepad++\notepad++.exe" set "NPP=%LOCALAPPDATA%\Programs\Notepad++\notepad++.exe"
:: ─── Открыть отчёт(ы) ──────────────────────────────────────
echo.
set /p "OPEN=  Открыть отчёт? [y/N] "
if /i "%OPEN%"=="y" (
if defined NPP (
echo   [i] Открываю в Notepad++ ...
for /f "usebackq delims=" %%F in ("%OUTLIST%") do (
if exist "%%F" start "" "%NPP%" "%%F"
)
) else (
echo   [i] Notepad++ не найден — открываю в Блокноте.
for /f "usebackq delims=" %%F in ("%OUTLIST%") do (
if exist "%%F" start "" notepad "%%F"
)
)
)
:: ── Убираем временный файл ──────────────────────────────────
if exist "%OUTLIST%" del "%OUTLIST%" >nul 2>&1
endlocal
pause