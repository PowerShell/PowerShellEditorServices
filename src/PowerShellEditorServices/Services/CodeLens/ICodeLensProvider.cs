// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Specifies the contract for a Code Lens provider.
    /// </summary>
    internal interface ICodeLensProvider
    {
        /// <summary>
        /// Specifies a unique identifier for the feature provider, typically a
        /// fully-qualified name like "Microsoft.PowerShell.EditorServices.MyProvider"
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Provides a collection of CodeLenses for the given
        /// document.
        /// </summary>
        /// <param name="scriptFile">
        /// The document for which CodeLenses should be provided.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns>An array of CodeLenses.</returns>
        CodeLens[] ProvideCodeLenses(ScriptFile scriptFile, CancellationToken cancellationToken);

        /// <summary>
        /// Resolves a CodeLens that was created without a Command.
        /// </summary>
        /// <param name="codeLens">
        /// The CodeLens to resolve.
        /// </param>
        /// <param name="scriptFile">
        /// The ScriptFile to resolve it in (sometimes unused).
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        /// A Task which returns the resolved CodeLens when completed.
        /// </returns>
        Task<CodeLens> ResolveCodeLens(
            CodeLens codeLens,
            ScriptFile scriptFile,
            CancellationToken cancellationToken);
    }
}
