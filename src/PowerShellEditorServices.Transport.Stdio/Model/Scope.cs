//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Model
{
    public class Scope
    {
//        /** name of the scope (as such 'Arguments', 'Locals') */
//        name: string;
        public string Name { get; set; }

//        /** The variables of this scope can be retrieved by passing the value of variablesReference to the VariablesRequest. */
//        variablesReference: number;
        public int VariablesReference { get; set; }

//        /** If true, the number of variables in this scope is large or expensive to retrieve. */
//        expensive: boolean;
        public bool Expensive { get; set; }

        public static Scope Create(VariableScope scope)
        {
            return new Scope
            {
                Name = scope.Name,
                VariablesReference = scope.Id
            };
        }
    }
}

