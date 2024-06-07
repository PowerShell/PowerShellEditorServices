$x = 1..10

function Renamed {
    param (
        $x
    )
    write-host "Printing $x"
}

foreach ($number in $x) {
    Renamed $number

    function testing_files {
        write-host "------------------"
    }
}
Renamed "99"
