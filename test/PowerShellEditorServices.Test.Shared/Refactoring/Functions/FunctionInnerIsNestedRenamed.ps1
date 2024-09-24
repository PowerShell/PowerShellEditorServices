function outer {
    function Renamed {
        Write-Host 'Inside nested foo'
    }
    Renamed
}

function foo {
    Write-Host "Inside top-level foo"
}

outer
foo
