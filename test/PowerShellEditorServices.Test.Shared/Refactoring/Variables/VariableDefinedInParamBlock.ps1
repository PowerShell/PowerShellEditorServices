$x = 1
function test {
    begin {
        $x = 5
    }
    process {
        $x
    }
    end {
        $x
    }
}
