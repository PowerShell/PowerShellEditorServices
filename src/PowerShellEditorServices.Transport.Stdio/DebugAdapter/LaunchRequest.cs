//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("launch")]
    public class LaunchRequest : RequestBase<LaunchRequestArguments>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // Execute the given PowerShell script and send the response.
            // Note that we aren't waiting for execution to complete here
            // because the debugger could stop while the script executes.
            editorSession.PowerShellSession
                .ExecuteScriptAtPath(this.Arguments.Program)
                .ContinueWith(
                    async (t) =>
                    {
                        Logger.Write(LogLevel.Verbose, "Execution completed, terminating...");

                        // TODO: Find a way to exit more gracefully!
                        await messageWriter.WriteMessage(new TerminatedEvent());
                        Environment.Exit(0);
                    });

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new LaunchResponse()));
        }
    }

    public class LaunchRequestArguments
    {
    //        /** An absolute path to the program to debug. */
        public string Program { get; set; }

    //        /** Automatically stop target after launch. If not specified, target does not stop. */
        public bool StopOnEntry { get; set; }

    //        /** Optional arguments passed to the debuggee. */
        public string[] Arguments { get; set; }

    //        /** Launch the debuggee in this working directory (specified as an absolute path). If omitted the debuggee is lauched in its own directory. */
        public string WorkingDirectory { get; set; }

    //        /** Absolute path to the runtime executable to be used. Default is the runtime executable on the PATH. */
        public string RuntimeExecutable { get; set; }

    //        /** Optional arguments passed to the runtime executable. */
        public string[] RuntimeArguments { get; set; }

    //        /** Optional environment variables to pass to the debuggee. The string valued properties of the 'environmentVariables' are used as key/value pairs. */
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}

