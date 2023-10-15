$x = 1..10

function Renamed {
    param (
        $x
    )
    write-host "Printing $x"
}

$x | ForEach-Object {
    Renamed $_

    function testing_files {
        write-host "------------------"
    }
}
Renamed "99"
