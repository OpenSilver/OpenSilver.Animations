@ECHO off

REM Packs the library with a version that never changes, and puts it in build\local-packages, next to
REM the OpenSilver packages of Build_Local_OpenSilver.bat. An app, like the Showcase, can then use the
REM library before it is published: it lists build\local-packages as a package source, and references
REM OpenSilver.Animations 0.1.0-preview-local.

SETLOCAL

IF "%~1" == "--help" GOTO :help
IF "%~1" == "-h" GOTO :help

SET LOCAL_PACKAGES=%~dp0local-packages
SET PackageVersion=0.1.0-preview-local
IF NOT "%~1" == "" SET PackageVersion=%~1

REM An empty OpenSilverVersion would replace the one in Directory.Build.props, so it's only passed when given.
SET OpenSilverProperty=
SET OpenSilverName=the version in Directory.Build.props
IF NOT "%~2" == "" (
	SET OpenSilverProperty=-p:OpenSilverVersion=%~2
	SET OpenSilverName=%~2
)

REM Define the escape character for colored text
FOR /F %%a IN ('"prompt $E$S & echo on & for %%b in (1) do rem"') DO SET "ESC=%%a"

ECHO. 
ECHO %ESC%[95mPacking %ESC%[0mOpenSilver.Animations %PackageVersion% %ESC%[95mfor %ESC%[0mOpenSilver %OpenSilverName%
ECHO. 
dotnet pack "%~dp0..\src\OpenSilver.Animations\OpenSilver.Animations.csproj" -c Release -p:PackageVersion=%PackageVersion% %OpenSilverProperty% -o "%LOCAL_PACKAGES%" -v:minimal || EXIT /b 1

REM The version number doesn't change between local builds, so NuGet would keep using its cached copy.
IF EXIST "%USERPROFILE%\.nuget\packages\opensilver.animations\%PackageVersion%" RMDIR /S /Q "%USERPROFILE%\.nuget\packages\opensilver.animations\%PackageVersion%"

ECHO %ESC%[92mDone. Rebuild the app to use OpenSilver.Animations %PackageVersion%.%ESC%[0m
EXIT /b 0

:help
ECHO [1] Package version (default: 0.1.0-preview-local)
ECHO [2] OpenSilver version (default: the one in Directory.Build.props)

ENDLOCAL
