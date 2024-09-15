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

/// <summary>
/// A handler for <a href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_prepareRename">textDocument/prepareRename</a>
/// LSP Ref: <see cref="PrepareRename()"/>
/// </summary>
internal class PrepareRenameHandler(WorkspaceService workspaceService) : IPrepareRenameHandler
{
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();

    public async Task<RangeOrPlaceholderRange> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);

        // TODO: Is this too aggressive? We can still rename inside a var/function even if dotsourcing is in use in a file, we just need to be clear it's not supported to take rename actions inside the dotsourced file.
        if (Utilities.AssertContainsDotSourced(scriptFile.ScriptAst))
        {
            throw new HandlerErrorException("Dot Source detected, this is currently not supported");
        }

        ScriptPosition scriptPosition = request.Position;
        int line = scriptPosition.Line;
        int column = scriptPosition.Column;

        // FIXME: Refactor out to utility when working

        // Cannot use generic here as our desired ASTs do not share a common parent
        Ast token = scriptFile.ScriptAst.Find(ast =>
        {
            // Supported types, filters out scriptblocks and whatnot
            if (ast is not (
                FunctionDefinitionAst
                or VariableExpressionAst
                or CommandParameterAst
                or ParameterAst
                or StringConstantExpressionAst
                or CommandAst
            ))
            {
                return false;
            }

            // Skip all statements that end before our target line or start after our target line
            if (ast.Extent.EndLineNumber < line || ast.Extent.StartLineNumber > line) { return false; }

            // Special detection for Function calls that dont follow verb-noun syntax e.g. DoThing
            // It's not foolproof but should work in most cases
            if (ast is StringConstantExpressionAst stringAst)
            {
                if (stringAst.Parent is not CommandAst parent) { return false; }
                // It will always be the first item in a defined command AST
                if (parent.CommandElements[0] != stringAst) { return false; }
            }

            Range astRange = new(
                ast.Extent.StartLineNumber,
                ast.Extent.StartColumnNumber,
                ast.Extent.EndLineNumber,
                ast.Extent.EndColumnNumber
            );
            return astRange.Contains(new Position(line, column));
        }, true);

        if (token is null) { return null; }

        Range astRange = new(
            token.Extent.StartLineNumber - 1,
            token.Extent.StartColumnNumber - 1,
            token.Extent.EndLineNumber - 1,
            token.Extent.EndColumnNumber - 1
        );

        return astRange;
    }
}

/// <summary>
/// A handler for textDocument/prepareRename
/// <para />LSP Ref: https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_rename
/// </summary>
internal class RenameHandler(WorkspaceService workspaceService) : IRenameHandler
{
    // RenameOptions may only be specified if the client states that it supports prepareSupport in its initial initialize request.
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();

    public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
    {


        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);
        ScriptPosition scriptPosition = request.Position;

        Ast tokenToRename = Utilities.GetAst(scriptPosition.Line, scriptPosition.Column, scriptFile.ScriptAst);

        ModifiedFileResponse changes = tokenToRename switch
        {
            FunctionDefinitionAst or CommandAst => RenameFunction(tokenToRename, scriptFile.ScriptAst, request),
            VariableExpressionAst => RenameVariable(tokenToRename, scriptFile.ScriptAst, request),
            // FIXME: Only throw if capability is not prepareprovider
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

/// <summary>
/// Represents a position in a script file. PowerShell script lines/columns start at 1, but LSP textdocument lines/columns start at 0.
/// </summary>
public record ScriptPosition(int Line, int Column)
{
    public static implicit operator ScriptPosition(Position position) => new(position.Line + 1, position.Character + 1);
    public static implicit operator Position(ScriptPosition position) => new() { Line = position.Line - 1, Character = position.Column - 1 };
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
