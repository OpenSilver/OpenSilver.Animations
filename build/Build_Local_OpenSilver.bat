@ECHO off

REM Builds the OpenSilver, OpenSilver.WebAssembly and OpenSilver.Simulator packages from a local
REM clone of the OpenSilver repository, and puts them in build\local-packages, which NuGet.Config
REM lists as a package source. Use it to work on the library and on OpenSilver at the same time, and
REM build with -p:OpenSilverVersion=3.4.0-preview-animations-local (or the version given) to use them.

SETLOCAL

IF "%~1" == "" GOTO :help
IF "%~1" == "--help" GOTO :help
IF "%~1" == "-h" GOTO :help

SET OPENSILVER_DIR=%~f1
SET LOCAL_PACKAGES=%~dp0local-packages
SET PackageVersion=3.4.0-preview-animations-local
IF NOT "%~2" == "" SET PackageVersion=%~2

REM Define the escape character for colored text
FOR /F %%a IN ('"prompt $E$S & echo on & for %%b in (1) do rem"') DO SET "ESC=%%a"

IF NOT EXIST "%OPENSILVER_DIR%\build\build-nuget-package-OpenSilver.bat" (
	ECHO %ESC%[91m"%OPENSILVER_DIR%" is not a clone of the OpenSilver repository.%ESC%[0m
	EXIT /b 1
)

CALL "%OPENSILVER_DIR%\build\build-nuget-package-OpenSilver.bat" %PackageVersion% || EXIT /b 1
CALL "%OPENSILVER_DIR%\build\build-nuget-package-OpenSilver.Wasm.bat" %PackageVersion% || EXIT /b 1
CALL "%OPENSILVER_DIR%\build\build-nuget-package-OpenSilver.Simulator.bat" %PackageVersion% || EXIT /b 1

ECHO. 
ECHO %ESC%[95mCopying the packages to %ESC%[0m%LOCAL_PACKAGES%
ECHO. 
COPY /Y "%OPENSILVER_DIR%\build\output\OpenSilver\OpenSilver.%PackageVersion%.nupkg" "%LOCAL_PACKAGES%" || EXIT /b 1
COPY /Y "%OPENSILVER_DIR%\build\output\OpenSilver\OpenSilver.WebAssembly.%PackageVersion%.nupkg" "%LOCAL_PACKAGES%" || EXIT /b 1
COPY /Y "%OPENSILVER_DIR%\build\output\OpenSilver\OpenSilver.Simulator.%PackageVersion%.nupkg" "%LOCAL_PACKAGES%" || EXIT /b 1

REM The version number doesn't change between local builds, so NuGet would keep using its cached copy.
FOR %%p IN (opensilver opensilver.webassembly opensilver.simulator) DO (
	IF EXIST "%USERPROFILE%\.nuget\packages\%%p\%PackageVersion%" RMDIR /S /Q "%USERPROFILE%\.nuget\packages\%%p\%PackageVersion%"
)

ECHO %ESC%[92mDone. Rebuild the solution with -p:OpenSilverVersion=%PackageVersion% to use OpenSilver %PackageVersion%.%ESC%[0m
EXIT /b 0

:help
ECHO [1] Path of a local clone of the OpenSilver repository (msbuild must be in the PATH)
ECHO [2] Package version (default: 3.4.0-preview-animations-local)

ENDLOCAL
