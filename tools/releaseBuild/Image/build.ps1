param ( [string]$target )
if ( ! (test-path ${target} ) ) {
    new-item -type directory ${target}
}
else {
    if ( test-path -pathtype leaf ${target} ) {
        remove-item -force ${target}
        new-item -type directory ${target}
    }
}
push-location C:/PowerShellEditorServices
Invoke-Build -Configuration Release
Copy-Item -Verbose -Recurse "C:/PowerShellEditorServices/module" "${target}/PowerShellEditorServices"
