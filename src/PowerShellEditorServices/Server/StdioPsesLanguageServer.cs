//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Host;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;

namespace Microsoft.PowerShell.EditorServices.Server
{
    internal class StdioPsesLanguageServer : PsesLanguageServer
    {
        internal StdioPsesLanguageServer(
            ILoggerFactory factory,
            LogLevel minimumLogLevel,
            HashSet<string> featureFlags,
            HostDetails hostDetails,
            string[] additionalModules,
            PSHost internalHost,
            ProfilePaths profilePaths) : base(
                factory,
                minimumLogLevel,
                // Stdio server can't support an integrated console so we pass in false.
                false,
                featureFlags,
                hostDetails,
                additionalModules,
                internalHost,
                profilePaths)
        {

        }

        protected override (Stream input, Stream output) GetInputOutputStreams()
        {
            return (System.Console.OpenStandardInput(), System.Console.OpenStandardOutput());
        }
    }
}
