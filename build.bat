:: RML Mod Build Script

@echo off
title RML Mod Build Script
for %%* in (.) do set foldername=%%~n*
mkdir "build"
robocopy "%foldername%" "build" /E /XF *.csproj *.rmod /XD bin obj
if exist "%foldername%.rmod" ( del "%foldername%.rmod" )
powershell "[System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem');[System.IO.Compression.ZipFile]::CreateFromDirectory(\"build\", \"%foldername%.rmod\", 0, 0)"
rmdir /s /q "build"
echo Build complete: %foldername%.rmod
