#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Close-VSCodeHtmlContentView {
    <#
    .SYNOPSIS
    Closes an HtmlContentView.

    .DESCRIPTION
    Closes an HtmlContentView inside of Visual Studio Code if
    it is displayed.

    .PARAMETER HtmlContentView
    The HtmlContentView to be closed.

    .EXAMPLE
    Close-VSCodeHtmlContentView -HtmlContentView $htmlContentView
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [Alias("View")]
        [ValidateNotNull()]
        [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentView]
        $HtmlContentView
    )

    process {
        $HtmlContentView.Close().Wait();
    }
}
