//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class ServerCapabilities
    {
        public TextDocumentSyncKind? TextDocumentSync { get; set; }

        public bool? HoverProvider { get; set; }

        public CompletionOptions CompletionProvider { get; set; }

        public SignatureHelpOptions SignatureHelpProvider { get; set; }

        public bool? DefinitionProvider { get; set; }

        public bool? ReferencesProvider { get; set; }

        public bool? DocumentHighlightProvider { get; set; }

        public bool? DocumentSymbolProvider { get; set; }

        public bool? WorkspaceSymbolProvider { get; set; }

        public bool? CodeActionProvider { get; set; }

        public CodeLensOptions CodeLensProvider { get; set; }

        public bool? DocumentFormattingProvider { get; set; }

        public bool? DocumentRangeFormattingProvider { get; set; }

        public DocumentOnTypeFormattingOptions DocumentOnTypeFormattingProvider { get; set; }

        public bool? RenameProvider { get; set; }

        public DocumentLinkOptions DocumentLinkProvider { get; set; }

        public ExecuteCommandOptions ExecuteCommandProvider { get; set; }

        public object Experimental { get; set; }
    }

    /// <summary>
    /// Execute command options.
    /// </summary>
    public class ExecuteCommandOptions
    {
        /// <summary>
        /// The commands to be executed on the server.
        /// </summary>
        public string[] Commands { get; set; }
    }

    /// <summary>
    /// Document link options.
    /// </summary>
    public class DocumentLinkOptions
    {
        /// <summary>
        /// Document links have a resolve provider.
        /// </summary>
        public bool? ResolveProvider { get; set; }
    }

    /// <summary>
    /// Options that the server provides for OnTypeFormatting request.
    /// </summary>
    public class DocumentOnTypeFormattingOptions
    {
        /// <summary>
        /// A character on which formatting should be triggered.
        /// </summary>
        public string FirstTriggerCharacter { get; set; }

        /// <summary>
        /// More trigger characters.
        /// </summary>
        public string[] MoreTriggerCharacters { get; set; }
    }

    /// <summary>
    /// Defines the document synchronization strategies that a server may support.
    /// </summary>
    public enum TextDocumentSyncKind
    {
        /// <summary>
        /// Indicates that documents should not be synced at all.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that document changes are always sent with the full content.
        /// </summary>
        Full,

        /// <summary>
        /// Indicates that document changes are sent as incremental changes after
        /// the initial document content has been sent.
        /// </summary>
        Incremental
    }

    public class CompletionOptions
    {
        public bool? ResolveProvider { get; set; }

        public string[] TriggerCharacters { get; set; }
    }

    public class SignatureHelpOptions
    {
        public string[] TriggerCharacters { get; set; }
    }
}

