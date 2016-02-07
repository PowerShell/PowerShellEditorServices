//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class LaunchRequest
    {
        public static readonly
            RequestType<LaunchRequestArguments, object> Type =
            RequestType<LaunchRequestArguments, object>.Create("launch");
    }

    public class LaunchRequestArguments
    {
    //        /** An absolute path to the program to debug. */
        public string Program { get; set; }

    //        /** Automatically stop target after launch. If not specified, target does not stop. */
        public bool StopOnEntry { get; set; }

    //        /** Optional arguments passed to the debuggee. */
        public string[] Args { get; set; }

    //        /** Launch the debuggee in this working directory (specified as an absolute path). If omitted the debuggee is lauched in its own directory. */
        public string Cwd { get; set; }

    //        /** Absolute path to the runtime executable to be used. Default is the runtime executable on the PATH. */
        public string RuntimeExecutable { get; set; }

    //        /** Optional arguments passed to the runtime executable. */
        public string[] RuntimeArguments { get; set; }

    //        /** Optional environment variables to pass to the debuggee. The string valued properties of the 'environmentVariables' are used as key/value pairs. */
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}

