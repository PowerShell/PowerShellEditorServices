$promptSawDebug = $false

function prompt {
    if (Test-Path variable:/PSDebugContext -ErrorAction SilentlyContinue) {
        $promptSawDebug = $true
    }

    return "$promptSawDebug > "
}

Write-Host "Debug over"
