//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    /// <summary>
    /// SetVariable request; value of command field is "setVariable".
    /// Request is initiated when user uses the debugger Variables UI to change the value of a variable.
    /// </summary>
    public class SetVariableRequest
    {
        public static readonly
            RequestType<SetVariableRequestArguments, SetVariableResponseBody, object, object> Type =
            RequestType<SetVariableRequestArguments, SetVariableResponseBody, object, object>.Create("setVariable");
    }

    [DebuggerDisplay("VariablesReference = {VariablesReference}")]
    public class SetVariableRequestArguments
    {
        public int VariablesReference { get; set; }

        public string Name { get; set; }

        public string Value { get; set; }
    }

    public class SetVariableResponseBody
    {
        public string Value { get; set; }
    }
}
