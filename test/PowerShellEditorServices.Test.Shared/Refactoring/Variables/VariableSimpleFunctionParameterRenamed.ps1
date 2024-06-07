$x = 1..10

function testing_files {

    param (
        [Alias("x")]$Renamed
    )
    write-host "Printing $Renamed"
}

foreach ($number in $x) {
    testing_files $number

    function testing_files {
        write-host "------------------"
    }
}
testing_files "99"
