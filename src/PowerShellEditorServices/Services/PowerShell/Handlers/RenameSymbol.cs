// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
namespace Microsoft.PowerShell.EditorServices.Handlers;

internal class RenameHandler(WorkspaceService workspaceService) : IRenameHandler
{
    // RenameOptions may only be specified if the client states that it supports prepareSupport in its initial initialize request.
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();


    public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);

        // AST counts from 1 whereas LSP counts from 0
        int line = request.Position.Line + 1;
        int column = request.Position.Character + 1;

        Ast tokenToRename = Utilities.GetAst(line, column, scriptFile.ScriptAst);

        ModifiedFileResponse changes = tokenToRename switch
        {
            FunctionDefinitionAst or CommandAst => RenameFunction(tokenToRename, scriptFile.ScriptAst, request),
            VariableExpressionAst => RenameVariable(tokenToRename, scriptFile.ScriptAst, request),
            _ => throw new HandlerErrorException("This should not happen as PrepareRename should have already checked for viability. File an issue if you see this.")
        };


        // TODO: Update changes to work directly and not require this adapter
        TextEdit[] textEdits = changes.Changes.Select(change => new TextEdit
        {
            Range = new Range
            {
                Start = new Position { Line = change.StartLine, Character = change.StartColumn },
                End = new Position { Line = change.EndLine, Character = change.EndColumn }
            },
            NewText = change.NewText
        }).ToArray();

        return new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [request.TextDocument.Uri] = textEdits
            }
        };
    }


    internal static ModifiedFileResponse RenameFunction(Ast token, Ast scriptAst, RenameParams requestParams)
    {
        RenameSymbolParams request = new()
        {
            FileName = requestParams.TextDocument.Uri.ToString(),
            Line = requestParams.Position.Line,
            Column = requestParams.Position.Character,
            RenameTo = requestParams.NewName
        };

        string tokenName = "";
        if (token is FunctionDefinitionAst funcDef)
        {
            tokenName = funcDef.Name;
        }
        else if (token.Parent is CommandAst CommAst)
        {
            tokenName = CommAst.GetCommandName();
        }
        IterativeFunctionRename visitor = new(tokenName,
                    request.RenameTo,
                    token.Extent.StartLineNumber,
                    token.Extent.StartColumnNumber,
                    scriptAst);
        visitor.Visit(scriptAst);
        ModifiedFileResponse FileModifications = new(request.FileName)
        {
            Changes = visitor.Modifications
        };
        return FileModifications;
    }

    internal static ModifiedFileResponse RenameVariable(Ast symbol, Ast scriptAst, RenameParams requestParams)
    {
        RenameSymbolParams request = new()
        {
            FileName = requestParams.TextDocument.Uri.ToString(),
            Line = requestParams.Position.Line,
            Column = requestParams.Position.Character,
            RenameTo = requestParams.NewName
        };
        if (symbol is VariableExpressionAst or ParameterAst or CommandParameterAst or StringConstantExpressionAst)
        {

            IterativeVariableRename visitor = new(
                request.RenameTo,
                symbol.Extent.StartLineNumber,
                symbol.Extent.StartColumnNumber,
                scriptAst,
                request.Options ?? null
            );
            visitor.Visit(scriptAst);
            ModifiedFileResponse FileModifications = new(request.FileName)
            {
                Changes = visitor.Modifications
            };
            return FileModifications;

        }
        return null;
    }
}

// {
//     [Serial, Method("powerShell/renameSymbol")]
//     internal interface IRenameSymbolHandler : IJsonRpcRequestHandler<RenameSymbolParams, RenameSymbolResult> { }

public class RenameSymbolOptions
{
    public bool CreateAlias { get; set; }
}

public class RenameSymbolParams : IRequest<RenameSymbolResult>
{
    public string FileName { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string RenameTo { get; set; }
    public RenameSymbolOptions Options { get; set; }
}
public class TextChange
{
    public string NewText { get; set; }
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}
public class ModifiedFileResponse
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
public class RenameSymbolResult
{
    public RenameSymbolResult() => Changes = new List<ModifiedFileResponse>();
    public List<ModifiedFileResponse> Changes { get; set; }
}

//     internal class RenameSymbolHandler : IRenameSymbolHandler
//     {
//         private readonly WorkspaceService _workspaceService;

//         public RenameSymbolHandler(WorkspaceService workspaceService) => _workspaceService = workspaceService;




//         public async Task<RenameSymbolResult> Handle(RenameSymbolParams request, CancellationToken cancellationToken)
//         {
//             // if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
//             // {
//             //     throw new InvalidOperationException("This should not happen as PrepareRename should have already checked for viability. File an issue if you see this.");
//             // }

//             return await Task.Run(() =>
//             {
//                 ScriptFile scriptFile = _workspaceService.GetFile(new Uri(request.FileName));
//                 Ast token = Utilities.GetAst(request.Line + 1, request.Column + 1, scriptFile.ScriptAst);
//                 if (token == null) { return null; }

//

//                 return result;
//             }).ConfigureAwait(false);
//         }
//     }
// }
