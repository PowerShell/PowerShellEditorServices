#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Write-VSCodeHtmlContentView {
    <#
    .SYNOPSIS
    Writes an HTML fragment to an HtmlContentView.

    .DESCRIPTION
    Writes an HTML fragment to an HtmlContentView.  This new fragment
    is appended to the existing content, useful in cases where the
    output will be appended to an ongoing output stream.

    .PARAMETER HtmlContentView
    The HtmlContentView where content will be appended.

    .PARAMETER AppendedHtmlBodyContent
    The HTML content that will be appended to the view's <body> element content.

    .EXAMPLE
    Write-VSCodeHtmlContentView -HtmlContentView $htmlContentView -AppendedHtmlBodyContent "<h3>Appended content</h3>"

    .EXAMPLE
    Write-VSCodeHtmlContentView -View $htmlContentView -Content "<h3>Appended content</h3>"
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [Alias("View")]
        [ValidateNotNull()]
        [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentView]
        $HtmlContentView,

        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Alias("Content")]
        [ValidateNotNull()]
        [string]
        $AppendedHtmlBodyContent
    )

    process {
        $HtmlContentView.AppendContent($AppendedHtmlBodyContent).Wait();
    }
}
