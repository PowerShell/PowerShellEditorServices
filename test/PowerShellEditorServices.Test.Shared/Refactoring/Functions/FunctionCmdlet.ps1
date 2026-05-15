function Testing-Foo {
    [CmdletBinding(SupportsShouldProcess)]
    param (
       $Text,
       $Param 
    )
    
    begin {
        if ($PSCmdlet.ShouldProcess("Target", "Operation")) {
            Testing-Foo -Text "Param" -Param [1,2,3]
        }
    }
    
    process {
        Testing-Foo -Text "Param" -Param [1,2,3]
    }
    
    end {
        Testing-Foo -Text "Param" -Param [1,2,3]
    }
}