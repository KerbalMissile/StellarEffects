@echo off
setlocal

set "ROOT=%~dp0.."
set "CSPROJ=%ROOT%\Source\StellarEffects.csproj"
set "NAME=StellarEffects"

set "VERSION="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "([xml](Get-Content -LiteralPath '%CSPROJ%')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"`) do set "VERSION=%%V"

if not defined VERSION (
    echo Could not read ^<Version^> from %CSPROJ%
    pause
    exit /b 1
)

echo Packaging %NAME% v%VERSION%
echo.

call "%~dp0build.bat" %*
if errorlevel 1 exit /b 1

set "STAGE=%ROOT%\Packages\stage"
set "DEST=%STAGE%\GameData\%NAME%"
set "ZIP=%ROOT%\Packages\%NAME%-v%VERSION%.zip"

if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ZIP%" del "%ZIP%"
mkdir "%DEST%"

xcopy "%ROOT%\Plugins" "%DEST%\Plugins" /e /i /y >nul
xcopy "%ROOT%\Configs" "%DEST%\Configs" /e /i /y >nul

tar -a -cf "%ZIP%" -C "%STAGE%" GameData
if errorlevel 1 (
    echo Failed to create zip.
    pause
    exit /b 1
)

rmdir /s /q "%STAGE%"

echo.
echo Done: Packages\%NAME%-v%VERSION%.zip

endlocal