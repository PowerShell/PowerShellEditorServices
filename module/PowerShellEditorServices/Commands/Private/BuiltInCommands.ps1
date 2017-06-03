Register-EditorCommand `
    -Name 'PowerShellEditorServices.OpenEditorProfile' `
    -DisplayName 'Open Editor Profile' `
    -SuppressOutput `
    -ScriptBlock {
        param([Microsoft.PowerShell.EditorServices.Extensions.EditorContext]$context)
        If (!(Test-Path -Path $Profile)) { New-Item -Path $Profile -ItemType File }
        $psEditor.Workspace.OpenFile($Profile)
    }

Register-EditorCommand `
    -Name 'PowerShellEditorServices.OpenProfileList' `
    -DisplayName 'Open Profile from List (Current User)' `
    -SuppressOutput `
    -ScriptBlock {
        param([Microsoft.PowerShell.EditorServices.Extensions.EditorContext]$context)
        
        $Current = Split-Path -Path $profile -Leaf        
        $List = @($Current,'Microsoft.VSCode_profile.ps1','Microsoft.PowerShell_profile.ps1','Microsoft.PowerShellISE_profile.ps1','Profile.ps1') | Select-Object -Unique
        $Choices = [System.Management.Automation.Host.ChoiceDescription[]] @($List)
        $Selection = $host.ui.PromptForChoice('Please Select a Profile', '(Current User)', $choices,'0')
        $Name = $List[$Selection]
        
        $ProfileDir = Split-Path $Profile -Parent
        $ProfileName = Join-Path -Path $ProfileDir -ChildPath $Name
        
        If (!(Test-Path -Path $ProfileName)) { New-Item -Path $ProfileName -ItemType File }
        
        $psEditor.Workspace.OpenFile($ProfileName)
    }