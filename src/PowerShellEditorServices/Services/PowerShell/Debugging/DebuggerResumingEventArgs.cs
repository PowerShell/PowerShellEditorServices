using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal record DebuggerResumingEventArgs(
        DebuggerResumeAction ResumeAction);
}
