function outer {
    function bar {
        Write-Host "Inside nested foo"
    }
    bar
}

function foo {
    Write-Host "Inside top-level foo"
}

outer
foo
