// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;

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
        public ModifiedFileResponse(string fileName)
        {
            FileName = fileName;
            Changes = new List<TextChange>();
        }

        public void AddTextChange(Ast Symbol, string NewText)
        {
            Changes.Add(
                new TextChange
                {
                    StartColumn = Symbol.Extent.StartColumnNumber - 1,
                    StartLine = Symbol.Extent.StartLineNumber - 1,
                    EndColumn = Symbol.Extent.EndColumnNumber - 1,
                    EndLine = Symbol.Extent.EndLineNumber - 1,
                    NewText = NewText
                }
            );
        }
    }
    internal class RenameSymbolResult
    {
        public RenameSymbolResult() => Changes = new List<ModifiedFileResponse>();
        public List<ModifiedFileResponse> Changes { get; set; }
    }

    internal class RenameSymbolHandler : IRenameSymbolHandler
    {
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public RenameSymbolHandler(ILoggerFactory loggerFactory,WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<RenameSymbolHandler>();
            _workspaceService = workspaceService;
        }
        internal static ModifiedFileResponse RefactorFunction(SymbolReference symbol, Ast scriptAst, RenameSymbolParams request)
        {
            if (symbol.Type is not SymbolType.Function)
            {
                return null;
            }

            FunctionRename visitor = new(symbol.NameRegion.Text,
                                        request.RenameTo,
                                        symbol.ScriptRegion.StartLineNumber,
                                        symbol.ScriptRegion.StartColumnNumber,
                                        scriptAst);
            scriptAst.Visit(visitor);
            ModifiedFileResponse FileModifications = new(request.FileName)
            {
                Changes = visitor.Modifications
            };
            return FileModifications;
        }
        public async Task<RenameSymbolResult> Handle(RenameSymbolParams request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
            {
                _logger.LogDebug("Failed to open file!");
                return await Task.FromResult<RenameSymbolResult>(null).ConfigureAwait(false);
            }
            // Locate the Symbol in the file
            // Look at its parent to find its script scope
            //  I.E In a function
            // Lookup all other occurances of the symbol
            // replace symbols that fall in the same scope as the initial symbol
            return await Task.Run(() =>
            {
                SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line + 1,
                    request.Column + 1);

                if (symbol == null) { return null; }

                Ast token = scriptFile.ScriptAst.Find(ast =>
                {
                    return ast.Extent.StartLineNumber == symbol.ScriptRegion.StartLineNumber &&
                    ast.Extent.StartColumnNumber == symbol.ScriptRegion.StartColumnNumber;
                }, true);
                ModifiedFileResponse FileModifications = null;
                if (symbol.Type is SymbolType.Function)
                {
                    FileModifications = RefactorFunction(symbol, scriptFile.ScriptAst, request);
                }

                RenameSymbolResult result = new();
                result.Changes.Add(FileModifications);
                return result;
            }).ConfigureAwait(false);
        }
    }
}
