#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Find-Ast {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [CmdletBinding(PositionalBinding=$false, DefaultParameterSetName='FilterScript')]
    param(
        [Parameter(Position=0, ParameterSetName='FilterScript')]
        [ValidateNotNullOrEmpty()]
        [scriptblock]
        $FilterScript = { $true },

        [Parameter(ValueFromPipeline, ValueFromPipelineByPropertyName, ParameterSetName='FilterScript')]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.Ast]
        $Ast,

        [Parameter(ParameterSetName='FilterScript')]
        [switch]
        $Before,

        [Parameter(ParameterSetName='FilterScript')]
        [switch]
        $Family,

        [Parameter(ParameterSetName='FilterScript')]
        [Alias('Closest', 'F')]
        [switch]
        $First,

        [Parameter(ParameterSetName='FilterScript')]
        [Alias('Furthest')]
        [switch]
        $Last,

        [Parameter(ParameterSetName='FilterScript')]
        [Alias('Parent')]
        [switch]
        $Ancestor,

        [Parameter(ParameterSetName='FilterScript')]
        [switch]
        $IncludeStartingAst,

        [Parameter(ParameterSetName='AtCursor')]
        [switch]
        $AtCursor
    )
    begin {
        # InvokeWithContext method is PS4+, but it's significantly faster for large files.
        if ($PSVersionTable.PSVersion.Major -ge 4) {

            $variableType = [System.Management.Automation.PSVariable]
            function InvokeWithContext {
                param([scriptblock]$Filter, [System.Management.Automation.Language.Ast]$DollarUnder)

                return $Filter.InvokeWithContext(
                        <# functionsToDefine: #> $null,
                        <# variablesToDefine: #> [Activator]::CreateInstance($variableType, @('_', $DollarUnder)),
                        <# args:              #> $aAst)
            }
        } else {
            $FilterScript = [scriptblock]::Create($FilterScript.ToString())
            function InvokeWithContext {
                param([scriptblock]$Filter, [System.Management.Automation.Language.Ast]$DollarUnder)

                return $DollarUnder | & { process { $Filter.InvokeReturnAsIs($DollarUnder) } }
            }
        }
        # Get all children or ancestors.
        function GetAllFamily {
            param($Start)

            if ($Before.IsPresent) {
                $parent = $Start
                for ($parent; $parent = $parent.Parent) { $parent }
                return
            }
            return $Start.FindAll({ $true }, $true)
        }
        # Get all asts regardless of structure, in either direction from the starting ast.
        function GetAllAsts {
            param($Start)

            $predicate = [Func[System.Management.Automation.Language.Ast,bool]]{
                $args[0] -ne $Ast
            }

            $topParent = Find-Ast -Ast $Start -Ancestor -Last -IncludeStartingAst
            if (-not $topParent) { $topParent = $Start }

            if ($Before.IsPresent) {
                # Need to store so we can reverse the collection.
                $result = [Linq.Enumerable]::TakeWhile(
                    $topParent.FindAll({ $true }, $true),
                    $predicate) -as [System.Management.Automation.Language.Ast[]]

                [array]::Reverse($result)
                return $result
            }
            return [Linq.Enumerable]::SkipWhile(
                $topParent.FindAll({ $true }, $true),
                $predicate)
        }
    }
    process {
        if ($Ancestor.IsPresent) {
            $Family = $Before = $true
        }
        $context = $psEditor.GetEditorContext()

        if (-not $Ast -and $context) {
            $Ast = $context.CurrentFile.Ast
        }
        switch ($PSCmdlet.ParameterSetName) {
            AtCursor {
                $cursorLine     = $context.CursorPosition.Line - 1
                $cursorColumn   = $context.CursorPosition.Column - 1
                $cursorOffset   = $Ast.Extent.Text |
                    Select-String "(.*\r?\n){$cursorLine}.{$cursorColumn}" |
                    ForEach-Object { $PSItem.Matches.Value.Length }

                # yield
                Find-Ast -Last {
                    $cursorOffset -ge $PSItem.Extent.StartOffset -and
                    $cursorOffset -le $PSItem.Extent.EndOffset
                }
            }
            FilterScript {
                if (-not $Ast) { return }

                # Check if we're trying to get the top level ancestor when we're already there.
                if ($Before.IsPresent -and
                    $Family.IsPresent -and
                    $Last.IsPresent   -and -not
                    $Ast.Parent       -and
                    $Ast -is [System.Management.Automation.Language.ScriptBlockAst])
                    { return $Ast }

                if ($Family.IsPresent) {
                    $asts = GetAllFamily $Ast
                } else {
                    $asts = GetAllAsts $Ast
                }
                # Check the first ast to see if it's our starting ast, unless
                $checkFirstAst = -not $IncludeStartingAst
                foreach ($aAst in $asts) {
                    if ($checkFirstAst) {
                        if ($aAst -eq $Ast) {
                            $checkFirstAst = $false
                            continue
                        }
                    }
                    $shouldReturn = InvokeWithContext $FilterScript $aAst

                    if (-not $shouldReturn) { continue }

                    # Return first, last, both, or all depending on the combination of switches.
                    if (-not $Last.IsPresent) {
                        $aAst # yield
                        if ($First.IsPresent) { break }
                    } else {
                        $lastMatch = $aAst
                        if ($First.IsPresent) {
                            $aAst # yield
                            $First = $false
                        }
                    }
                }
                # yield
                if ($Last.IsPresent) { return $lastMatch }
            }
        }
    }
}
