// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;
using System.Linq;

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

        public RenameSymbolHandler(ILoggerFactory loggerFactory, WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<RenameSymbolHandler>();
            _workspaceService = workspaceService;
        }
        internal static ModifiedFileResponse RenameFunction(Ast token, Ast scriptAst, RenameSymbolParams request)
        {
            if (token is FunctionDefinitionAst funcDef)
            {
                IterativeFunctionRename visitor = new(funcDef.Name,
                            request.RenameTo,
                            funcDef.Extent.StartLineNumber,
                            funcDef.Extent.StartColumnNumber,
                            scriptAst);
                visitor.Visit(scriptAst);
                ModifiedFileResponse FileModifications = new(request.FileName)
                {
                    Changes = visitor.Modifications
                };
                return FileModifications;

            }
            return null;

        }
        internal static ModifiedFileResponse RenameVariable(Ast symbol, Ast scriptAst, RenameSymbolParams request)
        {
            if (symbol is VariableExpressionAst or ParameterAst)
            {
                IterativeVariableRename visitor = new(request.RenameTo,
                                            symbol.Extent.StartLineNumber,
                                            symbol.Extent.StartColumnNumber,
                                            scriptAst);
                visitor.Visit(scriptAst);
                ModifiedFileResponse FileModifications = new(request.FileName)
                {
                    Changes = visitor.Modifications
                };
                return FileModifications;

            }
            return null;

        }
        public async Task<RenameSymbolResult> Handle(RenameSymbolParams request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
            {
                _logger.LogDebug("Failed to open file!");
                return await Task.FromResult<RenameSymbolResult>(null).ConfigureAwait(false);
            }

            return await Task.Run(() =>
            {

                IEnumerable<Ast> tokens = scriptFile.ScriptAst.FindAll(ast =>
                {
                    return request.Line+1 == ast.Extent.StartLineNumber &&
                           request.Column+1 >= ast.Extent.StartColumnNumber;
                }, false);

                Ast token = tokens.LastOrDefault();

                if (token == null) { return null; }

                ModifiedFileResponse FileModifications = token is FunctionDefinitionAst
                    ? RenameFunction(token, scriptFile.ScriptAst, request)
                    : RenameVariable(token, scriptFile.ScriptAst, request);

                RenameSymbolResult result = new();

                result.Changes.Add(FileModifications);

                return result;
            }).ConfigureAwait(false);
        }
    }
}
