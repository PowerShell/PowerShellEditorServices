//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class ServerCommand
    {
        /// <summary>
        /// Title of the command, like `save`.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The identifier of the actual command handler.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Arguments that the command handler should be
        /// invoked with.
        /// </summary>
        public JArray Arguments { get; set; }
    }
}