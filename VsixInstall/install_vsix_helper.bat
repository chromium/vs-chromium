REM Copyright 2013 The Chromium Authors. All rights reserved.
REM Use of this source code is governed by a BSD-style license that can be
REM found in the LICENSE file.

set VSIX_ID=a10cf7af-5f0a-4502-b44b-51ff1b7c8a87
set VSIX_FILENAME=VsChromium.vsix

REM ---------------------------------------------------
REM Detect location of VSIXInstaller.exe
set VSIX_INSTALLER_NAME=VSIXInstaller.exe

REM Order is important: Last (i.e. newer) one wins
for %%E in (Community Professional Enterprise) do (
  if exist "%ProgramFiles%\Microsoft Visual Studio\2022\%%E\Common7\IDE\%VSIX_INSTALLER_NAME%" set VSIX_INSTALLER=%ProgramFiles%\Microsoft Visual Studio\2022\%%E\Common7\IDE\%VSIX_INSTALLER_NAME%
)
for %%E in (Community Professional Enterprise) do (
  if exist "%ProgramFiles%\Microsoft Visual Studio\18\%%E\Common7\IDE\%VSIX_INSTALLER_NAME%" set VSIX_INSTALLER=%ProgramFiles%\Microsoft Visual Studio\18\%%E\Common7\IDE\%VSIX_INSTALLER_NAME%
)

if "%VSIX_INSTALLER%"=="" goto installer_not_found

REM ---------------------------------------------------
REM Analyse command line arguments

if "%1"=="-u" goto uninstall

if "%1"x==x goto noconfig
set CONFIG_NAME=%1

if "%2"x==x goto nosku
if "%3"x==x goto nosku
set SKU_NAME=%2
set SKU_VERSION=%3
goto install

REM ---------------------------------------------------
REM End of program
goto end

REM ---------------------------------------------------
REM Uninstall
:uninstall
"%VSIX_INSTALLER%" /u:%VSIX_ID%
goto end


REM ---------------------------------------------------
REM Install
:install
REM ---------------------------------------------------
REM Detect location of VSIX file
set PROJECT_LOCATION=%~dp0..\
if exist "%PROJECT_LOCATION%Binaries\%CONFIG_NAME%\%VSIX_FILENAME%" set VSIX_FILE="%PROJECT_LOCATION%Binaries\%CONFIG_NAME%\%VSIX_FILENAME%"
if "%VSIX_FILE%"=="" goto vsix_file_not_found

"%VSIX_INSTALLER%" /skuName:%SKU_NAME% /skuVersion:%SKU_VERSION% /u:%VSIX_ID% 
"%VSIX_INSTALLER%" /skuName:%SKU_NAME% /skuVersion:%SKU_VERSION% %VSIX_FILE%
goto end

:noconfig
echo "You must specify Debug or Release as the config name."
goto end

:nosku
echo "You must specify Visual Studio SKU name and version (17.0 for VS 2022, 18.0 for VS 2026)."
goto end

:installer_not_found
echo "VSIXInstaller.exe not found in regular Visual Studio 2022 or 2026 installation path."
goto end

:vsix_file_not_found
echo "VSIX file not found in output directories."
goto end


:end

