#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Show-VSCodeHtmlContentView {
    <#
    .SYNOPSIS
    Shows an HtmlContentView.

    .DESCRIPTION
    Shows an HtmlContentView that has been created and not shown
    yet or has previously been closed.

    .PARAMETER HtmlContentView
    The HtmlContentView that will be shown.

    .PARAMETER ViewColumn
    If specified, causes the new view to be displayed in the specified column.

    .EXAMPLE
    # Shows the view in the first editor column
    Show-VSCodeHtmlContentView -HtmlContentView $htmlContentView

    .EXAMPLE
    # Shows the view in the third editor column
    Show-VSCodeHtmlContentView -View $htmlContentView -Column Three
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [Alias("View")]
        [ValidateNotNull()]
        [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentView]
        $HtmlContentView,

        [Parameter(Mandatory = $false)]
        [Alias("Column")]
        [ValidateNotNull()]
        [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.ViewColumn]
        $ViewColumn = [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.ViewColumn]::One
    )

    process {
        $HtmlContentView.Show($ViewColumn).Wait()
    }
}
