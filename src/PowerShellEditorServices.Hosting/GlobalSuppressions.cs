// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Cmdlet parameters can be arrays", Scope = "module")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "PSES is not localized", Scope = "module")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "PSCmdlet.ThrowTerminatingError() is used instead", Scope = "member", Target = "~M:Microsoft.PowerShell.EditorServices.Commands.StartEditorServicesCommand.EndProcessing")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Checking user input from a configuration", Scope = "member", Target = "~M:Microsoft.PowerShell.EditorServices.Hosting.EditorServicesLoader.ValidateConfiguration")]
