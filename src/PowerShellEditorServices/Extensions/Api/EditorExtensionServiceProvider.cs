//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

using Internal = Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Extensions.Services
{
    /// <summary>
    /// Object to provide extension service APIs to extensions to PSES.
    /// </summary>
    public class EditorExtensionServiceProvider
    {
        private static readonly Assembly s_psesAsm = typeof(EditorExtensionServiceProvider).Assembly;

        private static readonly Lazy<object> s_psesAsmLoadContextLazy = new Lazy<object>(GetPsesAsmLoadContext);

        private static readonly Lazy<Type> s_asmLoadContextType = new Lazy<Type>(() => Type.GetType("System.Runtime.Loader.AssemblyLoadContext"));

        private static readonly Lazy<Func<IDisposable>> s_enterPsesReflectionContextLazy = new Lazy<Func<IDisposable>>(GetPsesAlcReflectionContextEntryFunc);

        private static readonly Lazy<Func<string, Assembly>> s_loadAssemblyInPsesAlc = new Lazy<Func<string, Assembly>>(GetPsesAlcLoadAsmFunc);

        private static Type AsmLoadContextType => s_asmLoadContextType.Value;

        private static object PsesAssemblyLoadContext => s_psesAsmLoadContextLazy.Value;

        private static Func<IDisposable> EnterPsesAlcReflectionContext => s_enterPsesReflectionContextLazy.Value;

        private static Func<string, Assembly> LoadAssemblyInPsesAlc => s_loadAssemblyInPsesAlc.Value;

        private readonly IServiceProvider _serviceProvider;

        internal EditorExtensionServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LanguageServer = new LanguageServerService(_serviceProvider.GetService<ILanguageServer>());
            //DocumentSymbols = new DocumentSymbolService(_serviceProvider.GetService<SymbolsService>());
            ExtensionCommands = new ExtensionCommandService(_serviceProvider.GetService<ExtensionService>());
            Workspace = new WorkspaceService(_serviceProvider.GetService<Internal.WorkspaceService>());
            EditorContext = new EditorContextService(_serviceProvider.GetService<ILanguageServer>());
            EditorUI = new EditorUIService(_serviceProvider.GetService<ILanguageServer>());
        }

        /// <summary>
        /// A service wrapper around the language server allowing sending notifications and requests to the LSP client.
        /// </summary>
        public ILanguageServerService LanguageServer { get; }

        /// <summary>
        /// Service providing document symbol provider registration.
        /// </summary>
        // public IDocumentSymbolService DocumentSymbols { get; }

        /// <summary>
        /// Service providing extension command registration and functionality.
        /// </summary>
        public IExtensionCommandService ExtensionCommands { get; }

        /// <summary>
        /// Service providing editor workspace functionality.
        /// </summary>
        public IWorkspaceService Workspace { get; }

        /// <summary>
        /// Service providing current editor context functionality.
        /// </summary>
        public IEditorContextService EditorContext { get; }

        /// <summary>
        /// Service providing editor UI functionality.
        /// </summary>
        public IEditorUIService EditorUI { get; }

        /// <summary>
        /// Get an underlying service object from PSES by type name.
        /// </summary>
        /// <param name="psesServiceFullTypeName">The full type name of the service to get.</param>
        /// <returns>The service object requested, or null if no service of that type name exists.</returns>
        /// <remarks>
        /// This method is intended as a trapdoor and should not be used in the first instance.
        /// Consider using the public extension services if possible.
        /// </remarks>
        public object GetService(string psesServiceFullTypeName) => GetService(psesServiceFullTypeName, "Microsoft.PowerShell.EditorServices");

        /// <summary>
        /// Get an underlying service object from PSES by type name.
        /// </summary>
        /// <param name="psesServiceFullTypeName">The full type name of the service to get.</param>
        /// <param name="assemblyName">The assembly name from which the service comes.</param>
        /// <returns>The service object requested, or null if no service of that type name exists.</returns>
        /// <remarks>
        /// This method is intended as a trapdoor and should not be used in the first instance.
        /// Consider using the public extension services if possible.
        /// </remarks>
        public object GetService(string fullTypeName, string assemblyName)
        {
            string asmQualifiedName = $"{fullTypeName}, {assemblyName}";
            return GetServiceByAssemblyQualifiedName(asmQualifiedName);
        }

        /// <summary>
        /// Get a PSES service by its fully assembly qualified name.
        /// </summary>
        /// <param name="asmQualifiedTypeName">The fully assembly qualified name of the service type to load.</param>
        /// <returns>The service corresponding to the given type, or null if none was found.</returns>
        /// <remarks>
        /// It's not recommended to run this method in parallel with anything,
        /// since the global reflection context change may have side effects in other threads.
        /// </remarks>
        public object GetServiceByAssemblyQualifiedName(string asmQualifiedTypeName)
        {
            Type serviceType;
            if (VersionUtils.IsNetCore)
            {
                using (EnterPsesAlcReflectionContext())
                {
                    serviceType = s_psesAsm.GetType(asmQualifiedTypeName);
                }
            }
            else
            {
                serviceType = Type.GetType(asmQualifiedTypeName);
            }

            return GetService(serviceType);
        }

        /// <summary>
        /// Get an underlying service object from PSES by type name.
        /// </summary>
        /// <param name="serviceType">The type of the service to fetch.</param>
        /// <returns>The service object requested, or null if no service of that type name exists.</returns>
        /// <remarks>
        /// This method is intended as a trapdoor and should not be used in the first instance.
        /// Consider using the public extension services if possible.
        /// 
        /// Also note that services in PSES may live in a separate assembly load context,
        /// meaning that a type of the seemingly correct name may fail to fetch to a service
        /// that is known under a type of the same name but loaded in a different context.
        /// </remarks>
        public object GetService(Type serviceType)
        {
            return _serviceProvider.GetService(serviceType);
        }

        /// <summary>
        /// Get the assembly load context the PSES loads its dependencies into.
        /// In .NET Framework, this returns null.
        /// </summary>
        /// <returns>The assembly load context used for loading PSES, or null in .NET Framework.</returns>
        public object GetPsesAssemblyLoadContext()
        {
            if (!VersionUtils.IsNetCore)
            {
                return null;
            }

            return PsesAssemblyLoadContext;
        }

        /// <summary>
        /// Load the given assembly in the PSES assembly load context.
        /// In .NET Framework, this simple loads the assembly in the LoadFrom context.
        /// </summary>
        /// <param name="assemblyPath">The absolute path of the assembly to load.</param>
        /// <returns>The loaded assembly object.</returns>
        public Assembly LoadAssemblyInPsesLoadContext(string assemblyPath)
        {
            if (!VersionUtils.IsNetCore)
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return LoadAssemblyInPsesAlc(assemblyPath);
        }

        private static Func<IDisposable> GetPsesAlcReflectionContextEntryFunc()
        {
            MethodInfo enterReflectionContextMethod = AsmLoadContextType.GetMethod("EnterContextualReflection", BindingFlags.Public | BindingFlags.Instance);

            return Expression.Lambda<Func<IDisposable>>(
                Expression.Convert(
                    Expression.Call(Expression.Constant(PsesAssemblyLoadContext), enterReflectionContextMethod),
                    typeof(IDisposable))).Compile();
        }

        private static Func<string, Assembly> GetPsesAlcLoadAsmFunc()
        {
            MethodInfo loadFromAssemblyPathMethod = AsmLoadContextType.GetMethod("LoadFromAssemblyPath", BindingFlags.Public | BindingFlags.Instance);
            return (Func<string, Assembly>)loadFromAssemblyPathMethod.CreateDelegate(typeof(Func<string, Assembly>), PsesAssemblyLoadContext);
        }

        private static object GetPsesAsmLoadContext()
        {
            if (!VersionUtils.IsNetCore)
            {
                return null;
            }

            Type alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext");
            MethodInfo getAlcMethod = alcType.GetMethod("GetLoadContext", BindingFlags.Public | BindingFlags.Static);
            return getAlcMethod.Invoke(obj: null, new object[] { s_psesAsm });
        }
    }
}
