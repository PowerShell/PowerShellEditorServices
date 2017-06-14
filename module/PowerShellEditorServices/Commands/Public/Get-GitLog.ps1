function Get-GitLog {
    $psEditor.Workspace.NewFile()
    git log --stat | Out-CurrentFile
}
