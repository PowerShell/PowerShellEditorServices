#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Out-CurrentFile {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory=$true)]
        $InputObject
    )

    Begin { $objectsToWrite = @() }
    Process { $objectsToWrite += $InputObject }
    End {
        $outputString = "@`"`r`n{0}`r`n`"@" -f ($objectsToWrite|out-string).Trim()
        $psEditor.GetEditorContext().CurrentFile.InsertText($outputString)
    }
}
