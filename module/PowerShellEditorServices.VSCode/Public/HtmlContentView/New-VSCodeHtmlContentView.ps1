#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function New-VSCodeHtmlContentView {
    <#
    .SYNOPSIS
    Creates a custom view in Visual Studio Code which displays HTML content.

    .DESCRIPTION
    Creates a custom view in Visual Studio Code which displays HTML content.

    .PARAMETER Title
    The title of the view.

    .PARAMETER ShowInColumn
    If specified, causes the new view to be displayed in the specified column.
    If unspecified, the Show-VSCodeHtmlContentView cmdlet will need to be used
    to display the view.

    .EXAMPLE
    # Create a new view called "My Custom View"
    $htmlContentView = New-VSCodeHtmlContentView -Title "My Custom View"

    .EXAMPLE
    # Create a new view and show it in the second view column
    $htmlContentView = New-VSCodeHtmlContentView -Title "My Custom View" -ShowInColumn Two
    #>
    [CmdletBinding()]
    [OutputType([Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentView])]
    param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Title,

        [Parameter(Mandatory = $false)]
        [Microsoft.PowerShell.EditorServices.VSCode.CustomViews.ViewColumn]
        $ShowInColumn
    )

    process {
        if ($psEditor -is [Microsoft.PowerShell.EditorServices.Extensions.EditorObject]) {
            $viewFeature = $psEditor.Components.Get([Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentViews])
            $view = $viewFeature.CreateHtmlContentView($Title).Result

            if ($ShowInColumn) {
                $view.Show($ShowInColumn).Wait();
            }

            return $view
        }
    }
}
