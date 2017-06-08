//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Specifies the contract for a Code Lens provider.
    /// </summary>
    public interface ICodeLensProvider : IProvider
    {
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
        /// <param name="cancellationToken">
        /// A CancellationToken which can be used to cancel the
        /// request.
        /// </param>
        /// <returns>
        /// A Task which returns the resolved CodeLens when completed.
        /// </returns>
        Task<CodeLens> ResolveCodeLensAsync(
            CodeLens codeLens,
            CancellationToken cancellationToken);
    }
}
