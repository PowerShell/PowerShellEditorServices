$x = 1
function test {
    begin {
        $Renamed = 5
    }
    process {
        $Renamed
    }
    end {
        $Renamed
    }
}
