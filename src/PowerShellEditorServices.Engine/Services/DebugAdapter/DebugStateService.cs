//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Engine.Services
{
    public class DebugStateService
    {
        internal bool NoDebug { get; set; }

        internal string Arguments { get; set; }

        internal bool IsRemoteAttach { get; set; }

        internal bool IsAttachSession { get; set; }

        internal bool WaitingForAttach { get; set; }

        internal string ScriptToLaunch { get; set; }

        internal bool OwnsEditorSession { get; set; }

        internal bool ExecutionCompleted { get; set; }

        internal bool IsInteractiveDebugSession { get; set; }

        internal bool SetBreakpointInProgress { get; set; }

        internal bool IsUsingTempIntegratedConsole { get; set; }
    }
}
