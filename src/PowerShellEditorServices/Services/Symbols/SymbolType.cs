// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        HashtableKey,

        /// <summary>
        /// The symbol is a class
        /// </summary>
        Class,

        /// <summary>
        /// The symbol is a enum
        /// </summary>
        Enum,

        /// <summary>
        /// The symbol is a class property
        /// </summary>
        Property,

        /// <summary>
        /// The symbol is a class method
        /// </summary>
        Method,

        /// <summary>
        /// The symbol is a class constructor
        /// </summary>
        Constructor,

        /// <summary>
        /// The symbol is a type reference
        /// </summary>
        Type,
    }
}
