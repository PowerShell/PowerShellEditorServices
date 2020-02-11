
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    public class EditorEngine
    {
        private static readonly Assembly s_psesAsm = typeof(EditorEngine).Assembly;

        private static readonly Lazy<Func<IDisposable>> s_enterPsesReflectionContextLazy = new Lazy<Func<IDisposable>>(GetPsesAlcReflectionContextEntryFunc);

        private static Func<IDisposable> EnterPsesAlcReflectionContext => s_enterPsesReflectionContextLazy.Value;

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
            if (PowerShellReflectionUtils.PSVersion.Major >= 6)
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

        private static Func<IDisposable> GetPsesAlcReflectionContextEntryFunc()
        {
            Type alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext");
            MethodInfo getAlcMethod = alcType.GetMethod("GetLoadContext", BindingFlags.Public | BindingFlags.Static);
            object psesAlc = getAlcMethod.Invoke(obj: null, new object[] { s_psesAsm });
            MethodInfo enterReflectionContextMethod = alcType.GetMethod("EnterContextualReflection", BindingFlags.Public | BindingFlags.Instance);

            return Expression.Lambda<Func<IDisposable>>(
                Expression.Convert(
                    Expression.Call(Expression.Constant(psesAlc), enterReflectionContextMethod),
                    typeof(IDisposable))).Compile();
        }
    }
}
