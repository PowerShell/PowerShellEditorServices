# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

function Set-ScriptExtent {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding(PositionalBinding = $false, DefaultParameterSetName = '__AllParameterSets')]
    param(
        [Parameter(Position = 0, Mandatory)]
        [psobject] $Text,

        [Parameter(Mandatory, ParameterSetName = 'AsString')]
        [switch]
        $AsString,

        [Parameter(Mandatory, ParameterSetName = 'AsArray')]
        [switch] $AsArray,

        [Parameter(ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [System.Management.Automation.Language.IScriptExtent] $Extent = (Find-Ast -AtCursor).Extent
    )
    begin {
        $fileContext = $psEditor.GetEditorContext().CurrentFile
        $descendingComparer = [System.Collections.Generic.Comparer[int]]::Create{
            param($x, $y) return $y.CompareTo($x)
        }

        $extentList = [System.Collections.Generic.SortedList[int, System.Management.Automation.Language.IScriptExtent]]::new(
            $descendingComparer)
    }
    process {
        if ($Extent -isnot [Microsoft.PowerShell.EditorServices.Extensions.FileScriptExtent, Microsoft.PowerShell.EditorServices]) {
            $Extent = [Microsoft.PowerShell.EditorServices.Extensions.FileScriptExtent, Microsoft.PowerShell.EditorServices]::FromOffsets(
                $fileContext,
                $Extent.StartOffset,
                $Extent.EndOffset)
        }

        $extentList.Add($Extent.StartOffset, $Extent)
    }
    end {
        $needsIndentFix = $false
        switch ($PSCmdlet.ParameterSetName) {
            # Insert text as a single string expression.
            AsString {
                $Text = "'{0}'" -f $Text.Replace("'", "''")
            }
            # Create a string expression for each line, separated by a comma.
            AsArray {
                $newLine = [Environment]::NewLine
                $Text = "'" + ($Text.Replace("'", "''") -split '\r?\n' -join "',$newLine'") + "'"

                if ($Text.Split("`n", [StringSplitOptions]::RemoveEmptyEntries).Count -gt 1) {
                    $needsIndentFix = $true
                }
            }
        }

        foreach ($kvp in $extentList.GetEnumerator()) {
            $aExtent = $kvp.Value
            $aText = $Text

            if ($needsIndentFix) {
                $indentOffset = ' ' * ($aExtent.StartColumnNumber - 1)
                $aText = $aText -split '\r?\n' -join ([Environment]::NewLine + $indentOffset)
            }

            $fileContext.InsertText($aText, $aExtent)
        }
    }
}
