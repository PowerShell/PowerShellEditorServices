using Microsoft.PowerShell.EditorServices.Hosting;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

#if CoreCLR
using System.Runtime.Loader;
#endif

namespace PowerShellEditorServices.Hosting
{
    public sealed class EditorServicesLoader : IDisposable
    {
        private const int Net461Version = 394254;

        private static readonly string s_psesDependencyDirPath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..",
                "Common"));

#if CoreCLR
        private static readonly AssemblyLoadContext s_coreAsmLoadContext = new PsesLoadContext(s_psesDependencyDirPath);
#endif

        public static EditorServicesLoader Create(
            EditorServicesConfig hostConfig,
            ISessionFileWriter sessionFileWriter,
            string dependencyPath = null)
        {
#if CoreCLR
            AssemblyLoadContext.Default.Resolving += DefaultLoadContext_OnAssemblyResolve;
#else
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_OnAssemblyResolve;
#endif

            return new EditorServicesLoader(hostConfig, sessionFileWriter);
        }

        private readonly EditorServicesConfig _hostConfig;

        private readonly ISessionFileWriter _sessionFileWriter;

        public EditorServicesLoader(EditorServicesConfig hostConfig, ISessionFileWriter sessionFileWriter)
        {
            _hostConfig = hostConfig;
            _sessionFileWriter = sessionFileWriter;
        }

        public async Task LoadAndRunEditorServicesAsync()
        {
            // Method with no implementation that forces the PSES assembly to load, triggering an AssemblyResolve event
            EditorServicesLoading.LoadEditorServicesForHost();

            using (var editorServicesRunner = EditorServicesRunner.Create(_hostConfig, _sessionFileWriter))
            {
                await editorServicesRunner.RunUntilShutdown().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            // TODO: Deregister assembly event
        }

#if CoreCLR
        private static Assembly DefaultLoadContext_OnAssemblyResolve(AssemblyLoadContext defaultLoadContext, AssemblyName asmName)
        {
            Console.WriteLine($".NET Core resolving {asmName}");

            if (!string.Equals(asmName.Name, "Microsoft.PowerShell.EditorServices", StringComparison.Ordinal))
            {
                return null;
            }

            string asmPath = Path.Combine(s_psesDependencyDirPath, $"{asmName.Name}.dll");

            return s_coreAsmLoadContext.LoadFromAssemblyPath(asmPath);
        }
#endif

#if !CoreCLR
        private static Assembly CurrentDomain_OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            Console.WriteLine($".NET FX resolving {args.Name}");

            var asmName = new AssemblyName(args.Name);

            string asmPath = Path.Combine(s_psesDependencyDirPath, $"{asmName.Name}.dll");
            
            return File.Exists(asmPath)
                ? Assembly.LoadFrom(asmPath)
                : null;
        }
#endif

    }
}
