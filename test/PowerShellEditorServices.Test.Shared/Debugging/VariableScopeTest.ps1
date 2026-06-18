$scopeTestVariable = "from parent scope"
& {
    $scopeTestVariable = "from local scope"
    Write-Output $scopeTestVariable
}
