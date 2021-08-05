# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

function Out-CurrentFile {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding()]
    param(
        [Switch]$AsNewFile,

        [Parameter(ValueFromPipeline, Mandatory = $true)]
        $InputObject
    )

    Begin { $objectsToWrite = @() }
    Process { $objectsToWrite += $InputObject }
    End {

        # If requested, create a new file
        if ($AsNewFile) {
            $psEditor.Workspace.NewFile()
        }

        $outputString = "@`"`r`n{0}`r`n`"@" -f ($objectsToWrite|out-string).Trim()

        try {
            # If there is no file open
            $psEditor.GetEditorContext()
        }
        catch {
            # create a new one
            $psEditor.Workspace.NewFile()
        }

        $psEditor.GetEditorContext().CurrentFile.InsertText($outputString)
    }
}
