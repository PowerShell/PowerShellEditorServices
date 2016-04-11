//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

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
        /// <summary>
        /// Gets or sets the absolute path to the program to debug.
        /// </summary>
        public string Program { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that indicates whether the script should be
        /// run with (false) or without (true) debugging support.
        /// </summary>
        public bool NoDebug { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that determines whether to automatically stop 
        /// target after launch. If not specified, target does not stop.
        /// </summary>
        public bool StopOnEntry { get; set; }

        /// <summary>
        /// Gets or sets optional arguments passed to the debuggee.
        /// </summary>
        public string[] Args { get; set; }

        /// <summary>
        /// Gets or sets the working directory of the launched debuggee (specified as an absolute path).
        /// If omitted the debuggee is lauched in its own directory.
        /// </summary>
        public string Cwd { get; set; }

        /// <summary>
        /// Gets or sets the absolute path to the runtime executable to be used. 
        /// Default is the runtime executable on the PATH.
        /// </summary>
        public string RuntimeExecutable { get; set; }

        /// <summary>
        /// Gets or sets the optional arguments passed to the runtime executable.
        /// </summary>
        public string[] RuntimeArgs { get; set; }

        /// <summary>
        /// Gets or sets optional environment variables to pass to the debuggee. The string valued 
        /// properties of the 'environmentVariables' are used as key/value pairs.
        /// </summary>
        public Dictionary<string, string> Env { get; set; }
    }
}

