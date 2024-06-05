$a = 1..5
$b = 6..10
function test {
    process {
        foreach ($Renamed in $a) {
            $Renamed
        }

        foreach ($testvar in $b) {
            $testvar
        }
    }
}
