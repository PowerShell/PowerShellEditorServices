// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Contains details pertaining to a variable scope in the current
    /// debugging session.
    /// </summary>
    internal class VariableScope
    {
        /// <summary>
        /// Gets a numeric ID that can be used in future operations
        /// relating to this scope.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets a name that describes the variable scope.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the VariableScope class with
        /// the given ID and name.
        /// </summary>
        /// <param name="id">The variable scope's ID.</param>
        /// <param name="name">The variable scope's name.</param>
        public VariableScope(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
