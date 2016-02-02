@echo off

REM This script is necessary until VS Code's debug adapter config allows command line parameters

setlocal
cd /d %~dp0

Microsoft.PowerShell.EditorServices.Host.x86.exe /debugAdapter