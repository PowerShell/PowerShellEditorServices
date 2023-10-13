// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;

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
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public PrepareRenameSymbolHandler(ILoggerFactory loggerFactory, WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<RenameSymbolHandler>();
            _workspaceService = workspaceService;
        }

        public async Task<PrepareRenameSymbolResult> Handle(PrepareRenameSymbolParams request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
            {
                _logger.LogDebug("Failed to open file!");
                return await Task.FromResult<PrepareRenameSymbolResult>(null).ConfigureAwait(false);
            }
            return await Task.Run(() =>
            {
                PrepareRenameSymbolResult result = new()
                {
                    message = ""
                };

                Ast token = scriptFile.ScriptAst.Find(ast =>
                {
                    return request.Line == ast.Extent.StartLineNumber &&
                        request.Column >= ast.Extent.StartColumnNumber && request.Column <= ast.Extent.EndColumnNumber;
                }, true);

                if (token == null) { result.message = "Unable to Find Symbol"; return result; }

                if (token is FunctionDefinitionAst funcDef)
                {
                    try
                    {

                        FunctionRename visitor = new(funcDef.Name,
                                    request.RenameTo,
                                    funcDef.Extent.StartLineNumber,
                                    funcDef.Extent.StartColumnNumber,
                                    scriptFile.ScriptAst);
                    }
                    catch (FunctionDefinitionNotFoundException)
                    {

                        result.message = "Failed to Find function definition within current file";
                    }
                }
                else if (token is VariableExpressionAst or CommandAst)
                {

                    try
                    {
                        VariableRename visitor = new(request.RenameTo,
                                            token.Extent.StartLineNumber,
                                            token.Extent.StartColumnNumber,
                                            scriptFile.ScriptAst);
                        if (visitor.TargetVariableAst == null)
                        {
                            result.message = "Failed to find variable definition within the current file";
                        }
                    }
                    catch (TargetVariableIsDotSourcedException)
                    {

                        result.message = "Variable is dot sourced which is currently not supported unable to perform a rename";
                    }

                }

                return result;
            }).ConfigureAwait(false);
        }
    }
}
