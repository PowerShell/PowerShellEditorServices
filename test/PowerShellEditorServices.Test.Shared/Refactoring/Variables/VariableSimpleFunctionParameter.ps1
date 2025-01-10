$x = 1..10

function testing_files {

    param (
        $x
    )
    Write-Host "Printing $x"
}

foreach ($number in $x) {
    testing_files $number

    function testing_files {
        Write-Host '------------------'
    }
}
testing_files '99'
