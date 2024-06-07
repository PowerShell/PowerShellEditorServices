function outer {
    function foo {
        Write-Host "Inside nested foo"
    }
    foo
}

function foo {
    Write-Host "Inside top-level foo"
}

outer
foo
