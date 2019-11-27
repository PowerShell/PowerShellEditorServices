
namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Implementation-free class designed to safely allow PowerShell Editor Services to be loaded in an obvious way.
    /// Referencing this class will force looking for and loading the PSES assembly if it's not already loaded.
    /// </summary>
    internal static class EditorServicesLoading
    {
        internal static void LoadEditorServicesForHost()
        {
            // No op that forces loading this assembly
        }
    }
}
