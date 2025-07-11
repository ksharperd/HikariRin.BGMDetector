@ECHO OFF
SETLOCAL

IF "%~1"=="" (
    GOTO :USAGE
)

IF "%~2"=="" (
    GOTO :USAGE
)

IF "%~3"=="" (
    GOTO :USAGE
)

SET vswherePath=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
IF NOT EXIST "%vswherePath%" GOTO :ERROR

SET toolsSuffix=x86.x64
IF /I "%~1"=="arm64" SET toolsSuffix=ARM64

FOR /F "tokens=*" %%i IN (
    '"%vswherePath%" -latest -prerelease -products * -property installationPath'
) DO SET vsBase=%%i

IF "%vsBase%"=="" GOTO :ERROR

SET procArch=%PROCESSOR_ARCHITEW6432%
IF "%procArch%"=="" SET procArch=%PROCESSOR_ARCHITECTURE%

SET vcEnvironment=%~1
IF /I "%~1"=="x64" (
    SET vcEnvironment=x86_amd64
    IF /I "%procArch%"=="AMD64" SET vcEnvironment=amd64
)
IF /I "%~1"=="arm64" (
    SET vcEnvironment=x86_arm64
    IF /I "%procArch%"=="AMD64" SET vcEnvironment=amd64_arm64
)
IF /I "%~1"=="x86" (
    IF /I "%procArch%"=="AMD64" SET vcEnvironment=amd64_x86
)

CALL "%vsBase%\Common7\Tools\VsDevCmd.bat" -startdir=none -arch=%~1 -host_arch=%~1

"%~2" "%~3"

EXIT /B 0

:USAGE
ECHO Usage: %~nx0 ^<arch^> ^<rc_compiler_dir^> ^<rc_dir^>
GOTO :ERROR

:ERROR
EXIT /B 1
