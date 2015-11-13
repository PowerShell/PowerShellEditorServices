//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("variables")]
    public class VariablesResponse : ResponseBase<VariablesResponseBody>
    {
        public static VariablesResponse Create(
            VariableDetails[] variables)
        {
            try
            {
                return new VariablesResponse
                {
                    Body = new VariablesResponseBody
                    {
                        Variables =
                            variables
                                .Select(Variable.Create)
                                .ToArray()
                    }
                };
            }
            catch (Exception e)
            {
                var mess = e.Message;
            }

            return null;
        }
    }

    public class VariablesResponseBody
    {
        public Variable[] Variables { get; set; }
    }
}

