// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;
using Microsoft.PowerShell.EditorServices.Services.Symbols;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/PrepareRenameSymbol")]
    internal interface IPrepareRenameSymbolHandler : IJsonRpcRequestHandler<PrepareRenameSymbolParams, PrepareRenameSymbolResult> { }

    internal class PrepareRenameSymbolParams : IRequest<PrepareRenameSymbolResult>
    {
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string RenameTo { get; set; }
    }
    internal class PrepareRenameSymbolResult
    {
        public string message;
    }

    internal class PrepareRenameSymbolHandler : IPrepareRenameSymbolHandler
    {
        private readonly WorkspaceService _workspaceService;

        public PrepareRenameSymbolHandler(WorkspaceService workspaceService) => _workspaceService = workspaceService;

        public async Task<PrepareRenameSymbolResult> Handle(PrepareRenameSymbolParams request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
            {
                // TODO: Unsaved file support. We need to find the unsaved file in the text documents synced to the LSP and use that as our Ast Base.
                throw new HandlerErrorException($"File {request.FileName} not found in workspace. Unsaved files currently do not support the rename symbol feature.");
            }
            return await Task.Run(() =>
            {
                PrepareRenameSymbolResult result = new()
                {
                    message = ""
                };
                // ast is FunctionDefinitionAst or CommandAst or VariableExpressionAst or StringConstantExpressionAst &&
                SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(request.Line + 1, request.Column + 1);
                Ast token = Utilities.GetAst(request.Line + 1, request.Column + 1, scriptFile.ScriptAst);

                if (token == null)
                {
                    result.message = "Unable to find symbol";
                    return result;
                }
                if (Utilities.AssertContainsDotSourced(scriptFile.ScriptAst))
                {
                    result.message = "Dot Source detected, this is currently not supported operation aborted";
                    return result;
                }

                bool IsFunction = false;
                string tokenName = "";

                switch (token)
                {

                    case FunctionDefinitionAst FuncAst:
                        IsFunction = true;
                        tokenName = FuncAst.Name;
                        break;
                    case VariableExpressionAst or CommandParameterAst or ParameterAst:
                        IsFunction = false;
                        tokenName = request.RenameTo;
                        break;
                    case StringConstantExpressionAst:

                        if (token.Parent is CommandAst CommAst)
                        {
                            IsFunction = true;
                            tokenName = CommAst.GetCommandName();
                        }
                        else
                        {
                            IsFunction = false;
                        }
                        break;
                }

                if (IsFunction)
                {
                    try
                    {
                        IterativeFunctionRename visitor = new(tokenName,
                            request.RenameTo,
                            token.Extent.StartLineNumber,
                            token.Extent.StartColumnNumber,
                            scriptFile.ScriptAst);
                    }
                    catch (FunctionDefinitionNotFoundException)
                    {
                        result.message = "Failed to Find function definition within current file";
                    }
                }
                else
                {
                    IterativeVariableRename visitor = new(tokenName,
                                        token.Extent.StartLineNumber,
                                        token.Extent.StartColumnNumber,
                                        scriptFile.ScriptAst);
                    if (visitor.TargetVariableAst == null)
                    {
                        result.message = "Failed to find variable definition within the current file";
                    }
                }
                return result;
            }).ConfigureAwait(false);
        }
    }
}
