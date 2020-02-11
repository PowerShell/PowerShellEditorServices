
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    public class EditorEngine
    {
        private static readonly Assembly s_psesAsm = typeof(EditorEngine).Assembly;

        private static readonly Lazy<object> s_psesAsmLoadContextLazy = new Lazy<object>(GetPsesAsmLoadContext);

        private static readonly Lazy<Type> s_asmLoadContextType = new Lazy<Type>(() => Type.GetType("System.Runtime.Loader.AssemblyLoadContext"));

        private static readonly Lazy<Func<IDisposable>> s_enterPsesReflectionContextLazy = new Lazy<Func<IDisposable>>(GetPsesAlcReflectionContextEntryFunc);

        private static readonly Lazy<Func<string, Assembly>> s_loadAssemblyInPsesAlc = new Lazy<Func<string, Assembly>>(GetPsesAlcLoadAsmFunc);

        private static Type AsmLoadContextType => s_asmLoadContextType.Value;

        private static object PsesAssemblyLoadContext => s_psesAsmLoadContextLazy.Value;

        private static Func<IDisposable> EnterPsesAlcReflectionContext => s_enterPsesReflectionContextLazy.Value;

        private static Func<string, Assembly> LoadAssemblyInPsesAlc => s_loadAssemblyInPsesAlc.Value;

        private readonly IServiceProvider _serviceProvider;

        internal EditorEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LanguageServer = new EditorLanguageServer(_serviceProvider.GetService<ILanguageServer>());
        }

        public EditorLanguageServer LanguageServer { get; }

        public object GetService(string psesServiceFullTypeName) => GetService(psesServiceFullTypeName, "Microsoft.PowerShell.EditorServices");

        public object GetService(string fullTypeName, string assemblyName)
        {
            string asmQualifiedName = $"{fullTypeName}, {assemblyName}";
            return GetServiceByAssemblyQualifiedName(asmQualifiedName);
        }

        public object GetService(Type serviceType)
        {
            return _serviceProvider.GetService(serviceType);
        }

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

            return _serviceProvider.GetService(serviceType);
        }

        public object GetPsesAssemblyLoadContext()
        {
            return PsesAssemblyLoadContext;
        }

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
