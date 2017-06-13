//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Components
{
    /// <summary>
    /// Provides generic helper methods for working with IComponentRegistry
    /// methods.
    /// </summary>
    public static class IComponentRegistryExtensions
    {
        /// <summary>
        /// Registers an instance of the specified component type
        /// or throws an ArgumentException if an instance has
        /// already been registered.
        /// </summary>
        /// <param name="componentRegistry">
        /// The IComponentRegistry instance.
        /// </param>
        /// <param name="componentInstance">
        /// The instance of the component to be registered.
        /// </param>
        /// <returns>
        /// The provided component instance for convenience in assignment
        /// statements.
        /// </returns>
        public static TComponent Register<TComponent>(
            this IComponentRegistry componentRegistry,
            TComponent componentInstance)
                where TComponent : class
        {
            return
                (TComponent)componentRegistry.Register(
                    typeof(TComponent),
                    componentInstance);
        }

        /// <summary>
        /// Gets the registered instance of the specified
        /// component type or throws a KeyNotFoundException if
        /// no instance has been registered.
        /// </summary>
        /// <param name="componentRegistry">
        /// The IComponentRegistry instance.
        /// </param>
        /// <returns>The implementation of the specified type.</returns>
        public static TComponent Get<TComponent>(
            this IComponentRegistry componentRegistry)
                where TComponent : class
        {
            return (TComponent)componentRegistry.Get(typeof(TComponent));
        }

        /// <summary>
        /// Attempts to retrieve the instance of the specified
        /// component type and, if found, stores it in the
        /// componentInstance parameter.
        /// </summary>
        /// <param name="componentRegistry">
        /// The IComponentRegistry instance.
        /// </param>
        /// <param name="componentInstance">
        /// The out parameter in which the found instance will be stored.
        /// </param>
        /// <returns>
        /// True if a registered instance was found, false otherwise.
        /// </returns>
        public static bool TryGet<TComponent>(
            this IComponentRegistry componentRegistry,
            out TComponent componentInstance)
                where TComponent : class
        {
            object componentObject = null;
            componentInstance = null;

            if (componentRegistry.TryGet(typeof(TComponent), out componentObject))
            {
                componentInstance = componentObject as TComponent;
                return componentInstance != null;
            }

            return false;
        }
    }
}