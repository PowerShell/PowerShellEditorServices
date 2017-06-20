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
