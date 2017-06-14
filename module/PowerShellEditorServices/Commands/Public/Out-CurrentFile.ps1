function Out-CurrentFile {
    param(
        [Parameter(ValueFromPipeline)]
        $data
    )

    Begin { $d = @() }
    Process { $d += $data }
    End {
        $target = "@`"`r`n{0}`r`n`"@" -f ($d|out-string).Trim()
        $pseditor.GetEditorContext().currentfile.inserttext($target)
    }
}