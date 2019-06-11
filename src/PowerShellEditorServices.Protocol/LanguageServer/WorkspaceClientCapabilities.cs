namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class WorkspaceClientCapabilities
    {
        /// <summary>
        /// The client supports applying batch edits to the workspace by
        /// by supporting the request `workspace/applyEdit'
        /// /// </summary>
        public bool? ApplyEdit { get; set; }


        /// <summary>
        /// Capabilities specific to `WorkspaceEdit`.
        /// </summary>
        public WorkspaceEditCapabilities WorkspaceEdit { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/didChangeConfiguration` notification.
        /// </summary>
        public DynamicRegistrationCapability DidChangeConfiguration { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/didChangeWatchedFiles` notification.
        /// </summary>
        public DynamicRegistrationCapability DidChangeWatchedFiles { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/symbol` request.
        /// </summary>
        public DynamicRegistrationCapability Symbol { get; set; }

        /// <summary>
        /// Capabilities specific to the `workspace/executeCommand` request.
        /// </summary>
        public DynamicRegistrationCapability ExecuteCommand { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to `WorkspaceEdit`.
    /// </summary>
    public class WorkspaceEditCapabilities
    {
        /// <summary>
        /// The client supports versioned document changes in `WorkspaceEdit`
        /// </summary>
        public bool? DocumentChanges { get; set; }
    }
}
