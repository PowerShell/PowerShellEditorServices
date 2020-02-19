//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// A way to define symbols on a higher level
    /// </summary>
    internal enum SymbolType
    {
        /// <summary>
        /// The symbol type is unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The symbol is a vairable
        /// </summary>
        Variable,

        /// <summary>
        /// The symbol is a function
        /// </summary>
        Function,

        /// <summary>
        /// The symbol is a parameter
        /// </summary>
        Parameter,

        /// <summary>
        /// The symbol is a DSC configuration
        /// </summary>
        Configuration,

        /// <summary>
        /// The symbol is a workflow
        /// </summary>
        Workflow,

        /// <summary>
        /// The symbol is a hashtable key
        /// </summary>
        HashtableKey
    }
}
