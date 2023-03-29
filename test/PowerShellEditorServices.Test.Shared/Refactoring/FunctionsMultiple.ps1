function One {
    write-host "One Hello World"
}
function Two {
    write-host "Two Hello World"
    One
}

function Three {
    write-host "Three Hello"
    Two
}

Function Four {
    Write-host "Four Hello"
    One
}
