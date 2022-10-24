if (-not $PROFILE) {
    throw
}

function Assert-ProfileLoaded {
	return $true
}

Register-EngineEvent -SourceIdentifier PowerShell.OnIdle -MaxTriggerCount 1 -Action { $global:handledInProfile = $true }
