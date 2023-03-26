// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/renameSymbol")]
    internal interface IRenameSymbolHandler : IJsonRpcRequestHandler<RenameSymbolParams, RenameSymbolResult> { }

    internal class RenameSymbolParams : IRequest<RenameSymbolResult>
    {
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string RenameTo { get; set; }
    }
    internal class TextChange
    {
        public string NewText { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }
    internal class ModifiedFileResponse
    {
        public string FileName { get; set; }
        public List<TextChange> Changes { get; set; }

    }
    internal class RenameSymbolResult
    {
        public List<ModifiedFileResponse> Changes { get; set; }
    }

    internal class RenameSymbolHandler : IRenameSymbolHandler
    {
        private readonly IInternalPowerShellExecutionService _executionService;

        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public RenameSymbolHandler(IInternalPowerShellExecutionService executionService,
        ILoggerFactory loggerFactory,
            WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<RenameSymbolHandler>();
            _workspaceService = workspaceService;
            _executionService = executionService;
        }

        /// Method to get a symbols parent function(s) if any
        internal static IEnumerable<Ast> GetParentFunction(SymbolReference symbol, Ast Ast)
        {
            return Ast.FindAll(ast =>
            {
                return ast.Extent.StartLineNumber <= symbol.ScriptRegion.StartLineNumber &&
                    ast.Extent.EndLineNumber >= symbol.ScriptRegion.EndLineNumber &&
                    ast is FunctionDefinitionAst;
            }, true);
        }
        internal static IEnumerable<Ast> GetVariablesWithinExtent(Ast symbol, Ast Ast)
        {
            return Ast.FindAll(ast =>
                {
                    return ast.Extent.StartLineNumber >= symbol.Extent.StartLineNumber &&
                    ast.Extent.EndLineNumber <= symbol.Extent.EndLineNumber &&
                    ast is VariableExpressionAst;
                }, true);
        }
        public async Task<RenameSymbolResult> Handle(RenameSymbolParams request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
            {
                _logger.LogDebug("Failed to open file!");
                return null;
            }
            // Locate the Symbol in the file
            // Look at its parent to find its script scope
            //  I.E In a function
            // Lookup all other occurances of the symbol
            // replace symbols that fall in the same scope as the initial symbol

            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(request.Line + 1, request.Column + 1);
            Ast ast = scriptFile.ScriptAst;

            RenameSymbolResult response = new()
            {
                Changes = new List<ModifiedFileResponse>
            {
                new ModifiedFileResponse()
                {
                    FileName = request.FileName,
                    Changes = new List<TextChange>()
                }
            }
            };

            foreach (Ast e in GetParentFunction(symbol, ast))
            {
                foreach (Ast v in GetVariablesWithinExtent(e, ast))
                {
                    TextChange change = new()
                    {
                        StartColumn = v.Extent.StartColumnNumber - 1,
                        StartLine = v.Extent.StartLineNumber - 1,
                        EndColumn = v.Extent.EndColumnNumber - 1,
                        EndLine = v.Extent.EndLineNumber - 1,
                        NewText = request.RenameTo
                    };
                    response.Changes[0].Changes.Add(change);
                }
            }

            PSCommand psCommand = new();
            psCommand
                .AddScript("Return 'Not sure how to make this non Async :('")
                .AddStatement();
            IReadOnlyList<string> result = await _executionService.ExecutePSCommandAsync<string>(psCommand, cancellationToken).ConfigureAwait(false);
            return response;
        }
    }
}
