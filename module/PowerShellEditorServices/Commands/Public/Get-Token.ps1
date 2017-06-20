#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Get-Token {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.Language.Token])]
    [System.Diagnostics.CodeAnalysis.SuppressMessage('PSUseOutputTypeCorrectly', '', Justification='Issue #676')]
    param(
        [Parameter(Position=0, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.IScriptExtent]
        $Extent
    )
    process {
        if (-not $Extent) {
            if (-not $PSCmdlet.MyInvocation.ExpectingInput) {
                # yield
                $psEditor.GetEditorContext().CurrentFile.Tokens
            }
            return
        }

        $tokens    = $psEditor.GetEditorContext().CurrentFile.Tokens
        $predicate = [Func[System.Management.Automation.Language.Token, bool]]{
            param($Token)

            ($Token.Extent.StartOffset -ge $Extent.StartOffset -and
             $Token.Extent.EndOffset   -le $Extent.EndOffset)
        }
        if ($tokens){
            $result = [Linq.Enumerable]::Where($tokens, $predicate)
        }
        # yield
        $result
    }
}
