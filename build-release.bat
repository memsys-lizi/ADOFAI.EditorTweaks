@echo off
setlocal

set "BUMP_KIND=%~1"
if "%BUMP_KIND%"=="" set "BUMP_KIND=Minor"

if /I not "%BUMP_KIND%"=="Major" if /I not "%BUMP_KIND%"=="Minor" if /I not "%BUMP_KIND%"=="Patch" (
    echo Invalid version bump kind: %BUMP_KIND%
    echo Usage: build-release.bat [Major^|Minor^|Patch]
    exit /b 1
)

pushd "%~dp0"
if errorlevel 1 exit /b 1

echo Building release package with %BUMP_KIND% version bump...
dotnet build "ADOFAI.EditorTweaks.csproj" -c Release /p:CreateModPackage=true /p:BumpModVersion=true /p:ModVersionBumpKind=%BUMP_KIND%
set "EXIT_CODE=%ERRORLEVEL%"

if "%EXIT_CODE%"=="0" (
    echo.
    echo Release build complete.
) else (
    echo.
    echo Release build failed with exit code %EXIT_CODE%.
)

popd
exit /b %EXIT_CODE%
