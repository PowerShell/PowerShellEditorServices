function Renamed {
    [CmdletBinding(SupportsShouldProcess)]
    param (
       $Text,
       $Param 
    )
    
    begin {
        if ($PSCmdlet.ShouldProcess("Target", "Operation")) {
            Renamed -Text "Param" -Param [1,2,3]
        }
    }
    
    process {
        Renamed -Text "Param" -Param [1,2,3]
    }
    
    end {
        Renamed -Text "Param" -Param [1,2,3]
    }
}