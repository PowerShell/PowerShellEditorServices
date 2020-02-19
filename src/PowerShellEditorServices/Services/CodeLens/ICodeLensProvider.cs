//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        /// <returns>An array of CodeLenses.</returns>
        CodeLens[] ProvideCodeLenses(ScriptFile scriptFile);

        /// <summary>
        /// Resolves a CodeLens that was created without a Command.
        /// </summary>
        /// <param name="codeLens">
        /// The CodeLens to resolve.
        /// </param>
        /// <param name="scriptFile">
        /// A CancellationToken which can be used to cancel the
        /// request.
        /// </param>
        /// <returns>
        /// A Task which returns the resolved CodeLens when completed.
        /// </returns>
        CodeLens ResolveCodeLens(
            CodeLens codeLens,
            ScriptFile scriptFile);
    }
}
