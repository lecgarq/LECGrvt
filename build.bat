@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\VsDevCmd.bat" -arch=x64 >nul 2>&1
dotnet --version
dotnet build LECG.csproj -c Debug
