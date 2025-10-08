$x = 1..10

function testing_files {

    param (
        $Renamed
    )
    Write-Host "Printing $Renamed"
}

foreach ($number in $x) {
    testing_files $number

    function testing_files {
        Write-Host '------------------'
    }
}
testing_files '99'
