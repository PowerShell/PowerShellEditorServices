$scriptBlock = {
    function FunctionInScriptBlock {
        Write-Host "Inside a script block"
    }
    FunctionInScriptBlock
}
& $scriptBlock
