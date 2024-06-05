$a = 1..5
$b = 6..10
function test {
    process {
        foreach ($testvar in $a) {
            $testvar
        }

        foreach ($testvar in $b) {
            $testvar
        }
    }
}
