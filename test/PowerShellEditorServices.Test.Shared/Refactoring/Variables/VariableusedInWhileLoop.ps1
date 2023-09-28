function Write-Item($itemCount) {
    $i = 1

    while ($i -le $itemCount) {
        $str = "Output $i"
        Write-Output $str

        # In the gutter on the left, right click and select "Add Conditional Breakpoint"
        # on the next line. Use the condition: $i -eq 25
        $i = $i + 1

        # Slow down execution a bit so user can test the "Pause debugger" feature.
        Start-Sleep -Milliseconds $DelayMilliseconds
    }
}
