namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class TextDocumentClientCapabilities
    {
        /// <summary>
        /// Synchronization capabilities the client supports.
        /// </summary>
        public SynchronizationCapabilities Synchronization { get; set; }

        /// <summary>
        /// Capabilities specific to `textDocument/completion`.
        /// </summary>
        public CompletionCapabilities Completion { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/hover`.
        /// </summary>
        public DynamicRegistrationCapability Hover { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/signatureHelp`.
        /// </summary>
        public DynamicRegistrationCapability SignatureHelp { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/references`.
        /// </summary>
        public DynamicRegistrationCapability References { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/documentHighlight`.
        /// </summary>
        public DynamicRegistrationCapability DocumentHighlight { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/documentSymbol`.
        /// </summary>
        public DynamicRegistrationCapability DocumentSymbol { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/formatting`.
        /// </summary>
        public DynamicRegistrationCapability Formatting { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/rangeFormatting`.
        /// </summary>
        public DynamicRegistrationCapability RangeFormatting { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/onTypeFormatting`.
        /// </summary>
        public DynamicRegistrationCapability OnTypeFormatting { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/definition`.
        /// </summary>
        public DynamicRegistrationCapability Definition { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/codeAction`.
        /// </summary>
        public DynamicRegistrationCapability CodeAction { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/codeLens`.
        /// </summary>
        public DynamicRegistrationCapability CodeLens { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/documentLink`.
        /// </summary>
        public DynamicRegistrationCapability DocumentLink { get; set; }

        /// <summary>
        /// Capabilities specific to the `textDocument/rename`.
        /// </summary>
        public DynamicRegistrationCapability Rename { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to `textDocument/completion`.
    /// </summary>
    public class CompletionCapabilities : DynamicRegistrationCapability
    {
        /// <summary>
        /// The client supports the following `CompletionItem` specific capabilities.
        /// </summary>
        /// <returns></returns>
        public CompletionItemCapabilities CompletionItem { get; set; }
    }

    /// <summary>
    /// Class to represent capabilities specific to `CompletionItem`.
    /// </summary>
    public class CompletionItemCapabilities
    {
        /// <summary>
        /// Client supports snippets as insert text.
        ///
        /// A snippet can define tab stops and placeholders with `$1`, `$2`
        /// and `${3:foo}`. `$0` defines the final tab stop, it defaults to
        /// the end of the snippet. Placeholders with equal identifiers are linked,
        /// that is typing in one will update others too.
        /// </summary>
        public bool? SnippetSupport { get; set; }
    }

    /// <summary>
    /// Class to represent synchronization capabilities the client supports.
    /// </summary>
    public class SynchronizationCapabilities : DynamicRegistrationCapability
    {
        /// <summary>
        /// The client supports sending will save notifications.
        /// </summary>
        public bool? WillSave { get; set; }

        /// <summary>
        /// The client supports sending a will save request and waits for a response
        /// providing text edits which will be applied to the document before it is save.
        /// </summary>
        public bool? WillSaveWaitUntil { get; set; }

        /// <summary>
        /// The client supports did save notifications.
        /// </summary>
        public bool? DidSave { get; set; }
    }
}
