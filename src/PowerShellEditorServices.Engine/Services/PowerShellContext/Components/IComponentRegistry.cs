//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Components
{
    /// <summary>
    /// Specifies the contract for a registry of component interfaces.
    /// </summary>
    public interface IComponentRegistry
    {
        /// <summary>
        /// Registers an instance of the specified component type
        /// or throws an ArgumentException if an instance has
        /// already been registered.
        /// </summary>
        /// <param name="componentType">
        /// The component type that the instance represents.
        /// </param>
        /// <param name="componentInstance">
        /// The instance of the component to be registered.
        /// </param>
        /// <returns>
        /// The provided component instance for convenience in assignment
        /// statements.
        /// </returns>
        object Register(
            Type componentType,
            object componentInstance);

        /// <summary>
        /// Gets the registered instance of the specified
        /// component type or throws a KeyNotFoundException if
        /// no instance has been registered.
        /// </summary>
        /// <param name="componentType">
        /// The component type for which an instance will be retrieved.
        /// </param>
        /// <returns>The implementation of the specified type.</returns>
        object Get(Type componentType);

        /// <summary>
        /// Attempts to retrieve the instance of the specified
        /// component type and, if found, stores it in the
        /// componentInstance parameter.
        /// </summary>
        /// <param name="componentType">
        /// The component type for which an instance will be retrieved.
        /// </param>
        /// <param name="componentInstance">
        /// The out parameter in which the found instance will be stored.
        /// </param>
        /// <returns>
        /// True if a registered instance was found, false otherwise.
        /// </returns>
        bool TryGet(Type componentType, out object componentInstance);
    }
}
