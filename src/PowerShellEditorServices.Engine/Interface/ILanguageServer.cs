using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Engine
{
    public interface ILanguageServer
    {
        Task StartAsync();

        Task WaitForShutdown();
    }
}
