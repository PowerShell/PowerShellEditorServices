#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Join-ScriptExtent {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.Language.IScriptExtent])]
    param(
        [Parameter(ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [System.Management.Automation.Language.IScriptExtent[]]
        $Extent
    )
    begin {
        $extentList = New-Object System.Collections.Generic.List[System.Management.Automation.Language.IScriptExtent]
    }
    process {
        if ($Extent) {
            $extentList.AddRange($Extent)
        }
    }
    end {
        if (-not $extentList) { return }

        $startOffset = [Linq.Enumerable]::Min($extentList.StartOffset -as [int[]])
        $endOffset   = [Linq.Enumerable]::Max($extentList.EndOffset -as [int[]])

        return New-Object Microsoft.PowerShell.EditorServices.FullScriptExtent @(
            $psEditor.GetEditorContext().CurrentFile,
            $startOffset,
            $endOffset)
    }
}
