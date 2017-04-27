namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class WorkspaceClientCapabilities
    {
        /// <summary>
        /// The client supports applying batch edits to the workspace by
        /// by supporting the request `workspace/applyEdit'
        /// /// </summary>
        bool ApplyEdit { get; set; }

        /// <summary>
        /// Capabilities specific to `WorkspaceEdit`.
        /// </summary>
        public WorkspaceEditCapabilities WorkspaceEdit { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/didChangeConfiguration` notification.
        /// </summary>
        public DidChangeConfigurationCapabilities DidChangeConfiguration { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/didChangeWatchedFiles` notification.
        /// </summary>
        public DidChangeWatchedFilesCapabilities DidChangeWatchedFiles { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/symbol` request.
        /// </summary>
        public SymbolCapabilities Symbol { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/executeCommand` request.
        /// </summary>
        public ExecuteCommandCapabilities ExecuteCommand { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to `WorkspaceEdit`.
    /// </summary>
    public class WorkspaceEditCapabilities
    {
        /// <summary>
        /// The client supports versioned document changes in `WorkspaceEdit`
        /// </summary>
        bool DocumentChanges { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to the `workspace/didChangeConfiguration` notification.
    /// </summary>
    public class DidChangeConfigurationCapabilities
    {
        /// <summary>
        /// Did change configuration supports dynamic registration.
        /// </summary>
        bool DynamicRegistration { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to the `workspace/didChangeWatchedFiles` notification.
    /// </summary>
    public class DidChangeWatchedFilesCapabilities
    {
        /// <summary>
        /// Did change watched files notification supports dynamic registration.
        /// </summary>
        bool DynamicRegistration { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to the `workspace/symbol` request.
    /// </summary>
    public class SymbolCapabilities
    {
        /// <summary>
        /// Symbol request supports dynamic registration.
        /// </summary>
        bool DynamicRegistration { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to the `workspace/executeCommand` request.
    /// </summary>
    public class ExecuteCommandCapabilities
    {
        /// <summary>
        /// Execute command supports dynamic registration.
        /// </summary>
        bool DynamicRegistration { get; set; }
    }
}
