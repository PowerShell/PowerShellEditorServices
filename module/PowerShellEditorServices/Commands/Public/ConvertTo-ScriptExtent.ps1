# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

function ConvertTo-ScriptExtent {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.Language.IScriptExtent])]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ParameterSetName = 'ByOffset')]
        [Alias('StartOffset', 'Offset')]
        [int] $StartOffsetNumber,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByOffset')]
        [Alias('EndOffset')]
        [int] $EndOffsetNumber,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByPosition')]
        [Alias('StartLine', 'Line')]
        [int] $StartLineNumber,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByPosition')]
        [Alias('StartColumn', 'Column')]
        [int] $StartColumnNumber,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByPosition')]
        [Alias('EndLine')]
        [int] $EndLineNumber,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByPosition')]
        [Alias('EndColumn')]
        [int] $EndColumnNumber,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByPosition')]
        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByOffset')]
        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByBuffer')]
        [Alias('File', 'FileName')]
        [string] $FilePath = $psEditor.GetEditorContext().CurrentFile.Path,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByBuffer')]
        [Alias('Start')]
        [Microsoft.PowerShell.EditorServices.Extensions.IFilePosition, Microsoft.PowerShell.EditorServices] $StartBuffer,

        [Parameter(ValueFromPipelineByPropertyName, ParameterSetName = 'ByBuffer')]
        [Alias('End')]
        [Microsoft.PowerShell.EditorServices.Extensions.IFilePosition, Microsoft.PowerShell.EditorServices] $EndBuffer,

        [Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName, ParameterSetName = 'ByExtent')]
        [System.Management.Automation.Language.IScriptExtent] $Extent
    )
    begin {
        $fileContext = $psEditor.GetEditorContext().CurrentFile
        $emptyExtent = [Microsoft.PowerShell.EditorServices.Extensions.FileScriptExtent, Microsoft.PowerShell.EditorServices]::Empty
    }
    process {
        # Already a InternalScriptExtent, FileScriptExtent or is empty.
        $returnAsIs = $Extent -and
            ($Extent.StartOffset -or $Extent.EndOffset -or $Extent -eq $emptyExtent)

        if ($returnAsIs) {
            return $Extent
        }

        if ($StartOffsetNumber) {
            $startOffset = $StartOffsetNumber
            $endOffset = $EndOffsetNumber

            # Allow creating a single position extent with just the offset parameter.
            if (-not $EndOffsetNumber) {
                $endOffset = $startOffset
            }

            return [Microsoft.PowerShell.EditorServices.Extensions.FileScriptExtent, Microsoft.PowerShell.EditorServices]::FromOffsets(
                $fileContext,
                $startOffset,
                $endOffset)
        }

        if ($StartBuffer) {
            if (-not $EndBuffer) {
                $EndBuffer = $StartBuffer
            }

            return [Microsoft.PowerShell.EditorServices.Extensions.FileScriptExtent, Microsoft.PowerShell.EditorServices]::FromPositions(
                $fileContext,
                $StartBuffer.Line,
                $StartBuffer.Column,
                $EndBuffer.Line,
                $EndBuffer.Column)
        }

        # Allow piping a single line and column to get a zero length script extent.
        if ($PSBoundParameters.ContainsKey('StartColumnNumber') -and -not $PSBoundParameters.ContainsKey('EndColumnNumber')) {
            $EndColumnNumber = $StartColumnNumber
        }

        if ($PSBoundParameters.ContainsKey('StartLineNumber') -and -not $PSBoundParameters.ContainsKey('EndLineNumber')) {
            $EndLineNumber = $StartLineNumber
        }

        # Protect against zero as a value since lines and columns start at 1
        if (-not $StartColumnNumber) {
            $StartColumnNumber = 1
        }

        if (-not $StartLineNumber) {
            $StartLineNumber = 1
        }

        if (-not $EndLineNumber) {
            $EndLineNumber = 1
        }

        if (-not $EndColumnNumber) {
            $EndColumnNumber = 1
        }

        return [Microsoft.PowerShell.EditorServices.Extensions.FileScriptExtent, Microsoft.PowerShell.EditorServices]::FromPositions(
            $fileContext,
            $StartLineNumber,
            $StartColumnNumber,
            $EndLineNumber,
            $EndColumnNumber)
    }
}
