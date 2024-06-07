function FunctionWithInternalCalls {
    Write-Host "This function calls itself"
    FunctionWithInternalCalls
}
FunctionWithInternalCalls
