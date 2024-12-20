$x = 1..10

function testing_files {
    param (
        $x
    )
    write-host "Printing $x"
}

$x | ForEach-Object {
    testing_files $_

    function testing_files {
        write-host "------------------"
    }
}
testing_files "99"
