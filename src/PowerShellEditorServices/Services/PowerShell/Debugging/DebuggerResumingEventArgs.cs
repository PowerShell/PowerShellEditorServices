// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal record DebuggerResumingEventArgs(
        DebuggerResumeAction ResumeAction);
}
