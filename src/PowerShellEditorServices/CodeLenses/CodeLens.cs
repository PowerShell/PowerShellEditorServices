//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Commands;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Defines the data for a "code lens" which is displayed
    /// above a symbol in a text document and has an associated
    /// command.
    /// </summary>
    public class CodeLens
    {
        /// <summary>
        /// Gets the ICodeLensProvider that created this CodeLens.
        /// </summary>
        public ICodeLensProvider Provider { get; private set; }

        /// <summary>
        /// Gets the ScriptFile for which the CodeLens was created.
        /// </summary>
        public ScriptFile File { get; private set; }

        /// <summary>
        /// Gets the IScriptExtent for the region which the CodeLens
        /// pertains.
        /// </summary>
        public IScriptExtent ScriptExtent { get; private set; }

        /// <summary>
        /// Gets the command which will be invoked in the editor
        /// when the CodeLens is clicked.
        /// </summary>
        public ClientCommand Command { get; private set; }

        /// <summary>
        /// Creates an instance of the CodeLens class.
        /// </summary>
        /// <param name="provider">
        /// The ICodeLensProvider which created this CodeLens.
        /// </param>
        /// <param name="scriptFile">
        /// The ScriptFile for which the CodeLens was created.
        /// </param>
        /// <param name="scriptExtent">
        /// The IScriptExtent for the region which the CodeLens
        /// pertains.
        /// </param>
        public CodeLens(
            ICodeLensProvider provider,
            ScriptFile scriptFile,
            IScriptExtent scriptExtent)
                : this(
                    provider,
                    scriptFile,
                    scriptExtent,
                    null)
        {
        }

        /// <summary>
        /// Creates an instance of the CodeLens class based on an
        /// original CodeLens instance, generally used when resolving
        /// the Command for a CodeLens.
        /// </summary>
        /// <param name="originalCodeLens">
        /// The original CodeLens upon which this instance is based.
        /// </param>
        /// <param name="resolvedCommand">
        /// The resolved ClientCommand for the original CodeLens.
        /// </param>
        public CodeLens(
            CodeLens originalCodeLens,
            ClientCommand resolvedCommand)
        {
            Validate.IsNotNull(nameof(originalCodeLens), originalCodeLens);
            Validate.IsNotNull(nameof(resolvedCommand), resolvedCommand);

            this.Provider = originalCodeLens.Provider;
            this.File = originalCodeLens.File;
            this.ScriptExtent = originalCodeLens.ScriptExtent;
            this.Command = resolvedCommand;
        }

        /// <summary>
        /// Creates an instance of the CodeLens class.
        /// </summary>
        /// <param name="provider">
        /// The ICodeLensProvider which created this CodeLens.
        /// </param>
        /// <param name="scriptFile">
        /// The ScriptFile for which the CodeLens was created.
        /// </param>
        /// <param name="scriptExtent">
        /// The IScriptExtent for the region which the CodeLens
        /// pertains.
        /// </param>
        /// <param name="command">
        /// The ClientCommand to execute when this CodeLens is clicked.
        /// If null, this CodeLens will be resolved by the editor when it
        /// gets displayed.
        /// </param>
        public CodeLens(
            ICodeLensProvider provider,
            ScriptFile scriptFile,
            IScriptExtent scriptExtent,
            ClientCommand command)
        {
            Validate.IsNotNull(nameof(provider), provider);
            Validate.IsNotNull(nameof(scriptFile), scriptFile);
            Validate.IsNotNull(nameof(scriptExtent), scriptExtent);

            this.Provider = provider;
            this.File = scriptFile;
            this.ScriptExtent = scriptExtent;
            this.Command = command;
        }
    }
}
