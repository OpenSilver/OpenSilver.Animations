@ECHO off

SETLOCAL

IF "%~1" == "--help" (
	GOTO :help
)

IF "%~1" == "-h" (
	GOTO :help
)

SET BUILD_DIR=%~dp0
SET SRC_DIR=%~dp0..\src\OpenSilver.Animations
SET CFG=Release

REM Define the escape character for colored text
FOR /F %%a IN ('"prompt $E$S & echo on & for %%b in (1) do rem"') DO SET "ESC=%%a"

REM Define the PackageVersion and OpenSilverPkgVersion variables
IF "%~1" == "" (
	SET /P PackageVersion="%ESC%[92mOpenSilver.Animations version:%ESC%[0m "
	SET /P OpenSilverPkgVersion="%ESC%[92mOpenSilver version:%ESC%[0m "
) ELSE (
	SET PackageVersion=%1
	IF "%~2" == "" (
		SET OpenSilverPkgVersion=%1
	) ELSE (
		SET OpenSilverPkgVersion=%2
	)
)

ECHO. 
ECHO %ESC%[95mBuilding and packing %ESC%[0mOpenSilver.Animations %ESC%[95min %ESC%[0m%CFG% %ESC%[95mconfiguration%ESC%[0m
ECHO. 
msbuild %SRC_DIR%\OpenSilver.Animations.csproj -t:Pack -p:Configuration=%CFG%;PackageVersion=%PackageVersion%;OpenSilverVersion=%OpenSilverPkgVersion%;PackageOutputPath=%BUILD_DIR%output -verbosity:minimal -restore

EXIT /b

:help
ECHO [1] OpenSilver.Animations version
ECHO [2] OpenSilver version (defaults to [1])

ENDLOCAL
