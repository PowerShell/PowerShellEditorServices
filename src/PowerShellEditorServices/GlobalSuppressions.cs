// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "PSES is not localized", Scope = "module")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by created object", Scope = "member", Target = "~M:Microsoft.PowerShell.EditorServices.Hosting.EditorServicesServerFactory.Create(System.String,System.Int32,System.IObservable{System.})~Microsoft.PowerShell.EditorServices.Hosting.EditorServicesServerFactory")]