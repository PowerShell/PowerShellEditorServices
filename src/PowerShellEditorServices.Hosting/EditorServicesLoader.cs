using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Server;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Hosting
{
    public sealed class EditorServicesLoader : IDisposable
    {
        private const int Net461Version = 394254;

        private static readonly string s_dependencyPath = null;

        public static EditorServicesLoader Create(EditorServicesConfig hostConfig, string dependencyPath = null)
        {
            // TODO: Register assembly resolve event

            return new EditorServicesLoader(hostConfig);
        }

        private readonly EditorServicesConfig _hostConfig;

        public EditorServicesLoader(EditorServicesConfig hostConfig)
        {
            _hostConfig = hostConfig;
        }

        public async Task LoadAndRunEditorServicesAsync()
        {
            // Method with no implementation that forces the PSES assembly to load, triggering an AssemblyResolve event
            EditorServicesLoading.LoadEditorServicesForHost();

            using (var editorServicesRunner = EditorServicesRunner.Create(_hostConfig))
            {
                await editorServicesRunner.RunUntilShutdown().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            // TODO: Deregister assembly event
        }
    }
}
