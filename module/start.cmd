%1 -NoProfile -NonInteractive -ExecutionPolicy Unrestricted ^
   -Command ".\Start-EditorServices.ps1 -HostName \"%2\" -HostProfileName \"%3\" -HostVersion \"%4\" -NamedPipeServerName \"%5\" -EditorServicesModulePath \"c:\dev\PowerShellEditorServices\module\PowerShellEditorServices\" -WaitForCompletion"

REM start.cmd "powershell.exe" "Visual Studio Code Host" "Microsoft.VSCode" "0.6.1" "PSES-VSCode-LanguageServer"