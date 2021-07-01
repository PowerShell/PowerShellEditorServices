
namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    internal interface IRunspaceContext
    {
        IRunspaceInfo CurrentRunspace { get; }
    }
}
