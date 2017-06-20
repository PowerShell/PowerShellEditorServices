#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Test-ScriptExtent {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [OutputType([bool], ParameterSetName='__AllParameterSets')]
    [OutputType([System.Management.Automation.Language.IScriptExtent], ParameterSetName='PassThru')]
    [CmdletBinding()]
    param(
        [Parameter(Position=0, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.IScriptExtent]
        $Extent,

        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.IScriptExtent]
        $Inside,

        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.IScriptExtent]
        $After,

        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.IScriptExtent]
        $Before,

        [Parameter(ParameterSetName='PassThru')]
        [switch]
        $PassThru
    )
    process {
        if (-not $Extent) { return $false }
        $passes = (-not $After  -or  $Extent.StartOffset -gt $After.EndOffset)    -and
                  (-not $Before -or  $Extent.EndOffset   -lt $Before.StartOffset) -and
                  (-not $Inside -or ($Extent.StartOffset -ge $Inside.StartOffset  -and
                                     $Extent.EndOffset   -le $Inside.EndOffset))

        if (-not $PassThru.IsPresent) { return $passes }

        if ($passes) {
            $Extent # yield
        }
    }
}
