@echo off
setlocal

set "ROOT=%~dp0.."
set "PROJECT=%ROOT%\Source\StellarEffects.csproj"

dotnet build "%PROJECT%" -c Release %*
if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

if exist "%ROOT%\Plugins\StellarEffects.dll" (
    echo.
    echo Done: Plugins\StellarEffects.dll
) else (
    echo.
    echo Build succeeded but Plugins\StellarEffects.dll was not found.
    pause
    exit /b 1
)

endlocal