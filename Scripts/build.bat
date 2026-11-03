@echo off
setlocal

set "ROOT=%~dp0.."
set "PROJECT=%ROOT%\Source\StellarEffects.csproj"
set "UNITY_PROJECT=%ROOT%\Unity\StellarEffectsShaders"
set "BUNDLE=%UNITY_PROJECT%\AssetBundles\StandaloneWindows64\stellareffects"
set "LOG=%ROOT%\Build\unity.log"

dotnet build "%PROJECT%" -c Release %*
if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

if not exist "%ROOT%\Plugins\StellarEffects.dll" (
    echo.
    echo Build succeeded but Plugins\StellarEffects.dll was not found.
    pause
    exit /b 1
)

if not exist "%UNITY_PROJECT%" (
    echo.
    echo Unity project not found at %UNITY_PROJECT%
    pause
    exit /b 1
)

if not defined UNITY_EXE (
    set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\2019.4.18f1\Editor\Unity.exe"
)

if not exist "%UNITY_EXE%" (
    echo.
    echo Unity 2019.4 not found. Set the UNITY_EXE environment variable to your Unity.exe.
    pause
    exit /b 1
)

if not exist "%ROOT%\Build" mkdir "%ROOT%\Build"
if exist "%BUNDLE%" del "%BUNDLE%"

echo.
echo Building shader bundle...
"%UNITY_EXE%" -batchmode -quit -projectPath "%UNITY_PROJECT%" -executeMethod StellarEffects.Editor.BuildBundles.Build -logFile "%LOG%"
if errorlevel 1 (
    echo Unity build failed. See Build\unity.log
    pause
    exit /b 1
)

if not exist "%BUNDLE%" (
    echo Unity finished but no bundle was produced. See Build\unity.log
    pause
    exit /b 1
)

if exist "%ROOT%\Plugins\stellareffects.bundle" del "%ROOT%\Plugins\stellareffects.bundle"
if not exist "%ROOT%\Shaders" mkdir "%ROOT%\Shaders"
copy /y "%BUNDLE%" "%ROOT%\Shaders\stellareffects.bundle" >nul

echo.
echo Done: Plugins\StellarEffects.dll
echo Done: Shaders\stellareffects.bundle

endlocal