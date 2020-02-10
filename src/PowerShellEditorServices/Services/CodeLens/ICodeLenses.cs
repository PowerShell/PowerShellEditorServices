//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Specifies the contract for an implementation of
    /// the ICodeLenses component.
    /// </summary>
    internal interface ICodeLenses
    {
        /// <summary>
        /// Gets the collection of ICodeLensProvider implementations
        /// that are registered with this component.
        /// </summary>
        List<ICodeLensProvider> Providers { get; }

        /// <summary>
        /// Provides a collection of CodeLenses for the given
        /// document.
        /// </summary>
        /// <param name="scriptFile">
        /// The document for which CodeLenses should be provided.
        /// </param>
        /// <returns>An array of CodeLenses.</returns>
        CodeLens[] ProvideCodeLenses(ScriptFile scriptFile);
    }
}
