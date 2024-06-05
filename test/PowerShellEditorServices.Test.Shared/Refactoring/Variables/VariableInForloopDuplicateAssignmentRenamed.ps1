$a = 1..5
$b = 6..10
function test {
    process {

        for ($Renamed = 0; $Renamed -lt $a.Count; $Renamed++) {
            $Renamed
        }

        for ($i = 0; $i -lt $a.Count; $i++) {
            $i
        }
    }
}
