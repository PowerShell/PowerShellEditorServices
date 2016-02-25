//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class Scope
    {
        /// <summary>
        /// Gets or sets the name of the scope (as such 'Arguments', 'Locals')
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the variables of this scope can be retrieved by passing the 
        /// value of variablesReference to the VariablesRequest.
        /// </summary>
        public int VariablesReference { get; set; }

        /// <summary>
        /// Gets or sets a boolean value indicating if number of variables in 
        /// this scope is large or expensive to retrieve. 
        /// </summary>
        public bool Expensive { get; set; }

        public static Scope Create(VariableScope scope)
        {
            return new Scope {
                Name = scope.Name,
                VariablesReference = scope.Id,
                // Temporary fix for #95 to get debug hover tips to work well at least for the local scope.
                Expensive = (scope.Name != VariableContainerDetails.LocalScopeName)
            };
        }
    }
}

