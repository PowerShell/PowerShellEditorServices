using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal class DebuggerResumingEventArgs
    {
        public DebuggerResumingEventArgs(DebuggerResumeAction resumeAction)
        {
            ResumeAction = resumeAction;
        }

        public DebuggerResumeAction ResumeAction { get; }

    }
}
