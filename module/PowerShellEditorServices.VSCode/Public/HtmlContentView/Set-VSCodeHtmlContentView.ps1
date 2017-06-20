#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Set-VSCodeHtmlContentView {
    <#
    .SYNOPSIS
    Sets the content of an HtmlContentView.

    .DESCRIPTION
    Sets the content of an HtmlContentView.  If an empty string
    is passed, it causes the view's content to be cleared.

    .PARAMETER HtmlContentView
    The HtmlContentView where content will be set.

    .PARAMETER HtmlBodyContent
    The HTML content that will be placed inside the <body> tag
    of the view.

    .EXAMPLE
    # Set the view content with an h1 header
    Set-VSCodeHtmlContentView -HtmlContentView $htmlContentView -HtmlBodyContent "<h1>Hello world!</h1>"

    .EXAMPLE
    # Clear the view
    Set-VSCodeHtmlContentView -View $htmlContentView -Content ""
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [Alias("View")]
        [ValidateNotNull()]
        [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentView]
        $HtmlContentView,

        [Parameter(Mandatory = $true)]
        [Alias("Content")]
        [AllowEmptyString()]
        [string]
        $HtmlBodyContent
    )

    process {
        $HtmlContentView.SetContent($HtmlBodyContent).Wait();
    }
}
