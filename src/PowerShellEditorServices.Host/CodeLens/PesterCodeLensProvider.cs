//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Commands;
using Microsoft.PowerShell.EditorServices.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    internal class PesterCodeLensProvider : FeatureProviderBase, ICodeLensProvider
    {
        /// <summary>
        /// The editor session context to provide CodeLenses for.
        /// </summary>
        private EditorSession _editorSession;

        /// <summary>
        /// The symbol provider to get symbols from to build code lenses with.
        /// </summary>
        private IDocumentSymbolProvider _symbolProvider;

        /// <summary>
        /// Pester 4.6.0 introduced a new ScriptblockFilter parameter to be able to run a test based on a line,
        /// therefore knowing this information is important.
        /// </summary>
        private readonly Lazy<Task<bool>> _pesterV4_6_0_OrHigherAvailable;

        /// <summary>
        /// Create a new Pester CodeLens provider for a given editor session.
        /// </summary>
        /// <param name="editorSession">The editor session context for which to provide Pester CodeLenses.</param>
        public PesterCodeLensProvider(EditorSession editorSession)
        {
            _editorSession = editorSession;
            _symbolProvider = new PesterDocumentSymbolProvider();
            _pesterV4_6_0_OrHigherAvailable = new Lazy<Task<bool>>(async () => await DeterminePesterVersion());
        }

        /// <summary>
        /// Get the Pester CodeLenses for a given Pester symbol.
        /// </summary>
        /// <param name="pesterSymbol">The Pester symbol to get CodeLenses for.</param>
        /// <param name="scriptFile">The script file the Pester symbol comes from.</param>
        /// <returns>All CodeLenses for the given Pester symbol.</returns>
        private async Task<CodeLens[]> GetPesterLens(
            PesterSymbolReference pesterSymbol,
            ScriptFile scriptFile)
        {
            // A value of null is a signal to PSES that the available Pester version does not support
            // running Describe blocks by name (the test name will used instead then).
            int? describeBlockLineNumber = await _pesterV4_6_0_OrHigherAvailable.Value ? (int?)pesterSymbol.ScriptRegion.StartLineNumber : null;
            var codeLensResults = new CodeLens[]
            {
                new CodeLens(
                    this,
                    scriptFile,
                    pesterSymbol.ScriptRegion,
                    new ClientCommand(
                        "PowerShell.RunPesterTests",
                        "Run tests",
                        new object[] { scriptFile.ClientFilePath, false /* No debug */, pesterSymbol.TestName, describeBlockLineNumber })),

                new CodeLens(
                    this,
                    scriptFile,
                    pesterSymbol.ScriptRegion,
                    new ClientCommand(
                        "PowerShell.RunPesterTests",
                        "Debug tests",
                        new object[] { scriptFile.ClientFilePath, true /* Run in debugger */, pesterSymbol.TestName, describeBlockLineNumber })),
            };

            return codeLensResults;
        }

        /// <summary>
        /// Used to determine the value of <see cref="_pesterV4_6_0_OrHigherAvailable"/> as a background task.
        /// </summary>
        private async Task<bool> DeterminePesterVersion()
        {
            //if (System.Diagnostics.Debugger.IsAttached)
            //{
            //    System.Diagnostics.Debugger.Break();
            //}
            //else
            //{
            //    System.Diagnostics.Debugger.Launch();
            //}
            var powerShell = new PSCommand()
                .AddCommand("Get-Command")
                .AddParameter("Name", "Invoke-Pester");

            IEnumerable<PSObject> result = await _editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(powerShell);
            var pesterCommand = result.FirstOrDefault();
            if (pesterCommand == null)
            {
                return false;
            }

            if (pesterCommand.BaseObject is FunctionInfo invokePesterFunction)
            {
                var pesterVersion = invokePesterFunction.Version;
                if (pesterVersion.Major >= 4 && pesterVersion.Minor >= 6)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get all Pester CodeLenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The script file to get Pester CodeLenses for.</param>
        /// <returns>All Pester CodeLenses for the given script file.</returns>
        public async Task<CodeLens[]> ProvideCodeLenses(ScriptFile scriptFile)
        {
            var lenses = new List<CodeLens>();
            foreach (SymbolReference symbol in _symbolProvider.ProvideDocumentSymbols(scriptFile))
            {
                if (symbol is PesterSymbolReference pesterSymbol)
                {
                    if (pesterSymbol.Command != PesterCommandType.Describe)
                    {
                        continue;
                    }

                    lenses.AddRange(await GetPesterLens(pesterSymbol, scriptFile));
                }
            }

            return lenses.ToArray();
        }

        /// <summary>
        /// Resolve the CodeLens provision asynchronously -- just wraps the CodeLens argument in a task.
        /// </summary>
        /// <param name="codeLens">The code lens to resolve.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The given CodeLens, wrapped in a task.</returns>
        public Task<CodeLens> ResolveCodeLensAsync(
            CodeLens codeLens,
            CancellationToken cancellationToken)
        {
            // This provider has no specific behavior for
            // resolving CodeLenses.
            return Task.FromResult(codeLens);
        }
    }
}
