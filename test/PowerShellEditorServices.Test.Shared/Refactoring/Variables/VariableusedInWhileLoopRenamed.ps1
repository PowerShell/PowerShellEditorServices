function Write-Item($itemCount) {
    $Renamed = 1

    while ($Renamed -le $itemCount) {
        $str = "Output $Renamed"
        Write-Output $str

        # In the gutter on the left, right click and select "Add Conditional Breakpoint"
        # on the next line. Use the condition: $i -eq 25
        $Renamed = $Renamed + 1

        # Slow down execution a bit so user can test the "Pause debugger" feature.
        Start-Sleep -Milliseconds $DelayMilliseconds
    }
}
