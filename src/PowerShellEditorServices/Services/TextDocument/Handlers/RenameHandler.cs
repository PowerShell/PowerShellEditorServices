// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers;

/// <summary>
/// A handler for <a href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_prepareRename">textDocument/prepareRename</a>
/// LSP Ref: <see cref="PrepareRename()"/>
/// </summary>
internal class PrepareRenameHandler(WorkspaceService workspaceService, ILanguageServerFacade lsp, ILanguageServerConfiguration config) : IPrepareRenameHandler
{
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();

    public async Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        // FIXME: Config actually needs to be read and implemented, this is to make the referencing satisfied
        config.ToString();
        ShowMessageRequestParams reqParams = new ShowMessageRequestParams
        {
            Type = MessageType.Warning,
            Message = "Test Send",
            Actions = new MessageActionItem[] {
                new MessageActionItem() { Title = "I Accept" },
                new MessageActionItem() { Title = "I Accept [Workspace]" },
                new MessageActionItem() { Title = "Decline" }
            }
        };

        MessageActionItem result = await lsp.SendRequest(reqParams, cancellationToken).ConfigureAwait(false);
        if (result.Title == "Test Action")
        {
            // FIXME: Need to accept
            Console.WriteLine("yay");
        }

        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);

        // TODO: Is this too aggressive? We can still rename inside a var/function even if dotsourcing is in use in a file, we just need to be clear it's not supported to take rename actions inside the dotsourced file.
        if (Utilities.AssertContainsDotSourced(scriptFile.ScriptAst))
        {
            throw new HandlerErrorException("Dot Source detected, this is currently not supported");
        }

        ScriptPositionAdapter position = request.Position;
        Ast target = FindRenamableSymbol(scriptFile, position);
        if (target is null) { return null; }
        return target switch
        {
            FunctionDefinitionAst funcAst => GetFunctionNameExtent(funcAst),
            _ => new ScriptExtentAdapter(target.Extent)
        };
    }

    private static ScriptExtentAdapter GetFunctionNameExtent(FunctionDefinitionAst ast)
    {
        string name = ast.Name;
        // FIXME: Gather dynamically from the AST and include backticks and whatnot that might be present
        int funcLength = "function ".Length;
        ScriptExtentAdapter funcExtent = new(ast.Extent);

        // Get a range that represents only the function name
        return funcExtent with
        {
            Start = funcExtent.Start.Delta(0, funcLength),
            End = funcExtent.Start.Delta(0, funcLength + name.Length)
        };
    }

    /// <summary>
    /// Finds a renamable symbol at a given position in a script file.
    /// </summary>
    /// <returns>Ast of the token or null if no renamable symbol was found</returns>
    internal static Ast FindRenamableSymbol(ScriptFile scriptFile, ScriptPositionAdapter position)
    {
        int line = position.Line;
        int column = position.Column;

        // Cannot use generic here as our desired ASTs do not share a common parent
        Ast token = scriptFile.ScriptAst.Find(ast =>
        {
            // Skip all statements that end before our target line or start after our target line. This is a performance optimization.
            if (ast.Extent.EndLineNumber < line || ast.Extent.StartLineNumber > line) { return false; }

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

            // Special detection for Function calls that dont follow verb-noun syntax e.g. DoThing
            // It's not foolproof but should work in most cases where it is explicit (e.g. not & $x)
            if (ast is StringConstantExpressionAst stringAst)
            {
                if (stringAst.Parent is not CommandAst parent) { return false; }
                if (parent.GetCommandName() != stringAst.Value) { return false; }
            }

            ScriptExtentAdapter target = ast switch
            {
                FunctionDefinitionAst funcAst => GetFunctionNameExtent(funcAst),
                _ => new ScriptExtentAdapter(ast.Extent)
            };

            return target.Contains(position);
        }, true);

        return token;
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

    public async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);
        ScriptPositionAdapter position = request.Position;

        Ast tokenToRename = PrepareRenameHandler.FindRenamableSymbol(scriptFile, position);
        if (tokenToRename is null) { return null; }

        // TODO: Potentially future cross-file support
        TextEdit[] changes = tokenToRename switch
        {
            FunctionDefinitionAst or CommandAst => RenameFunction(tokenToRename, scriptFile.ScriptAst, request),
            VariableExpressionAst => RenameVariable(tokenToRename, scriptFile.ScriptAst, request),
            // FIXME: Only throw if capability is not prepareprovider
            _ => throw new HandlerErrorException("This should not happen as PrepareRename should have already checked for viability. File an issue if you see this.")
        };

        return new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [request.TextDocument.Uri] = changes
            }
        };
    }

    // TODO: We can probably merge these two methods with Generic Type constraints since they are factored into overloading

    internal static TextEdit[] RenameFunction(Ast token, Ast scriptAst, RenameParams renameParams)
    {
        ScriptPositionAdapter position = renameParams.Position;

        string tokenName = "";
        if (token is FunctionDefinitionAst funcDef)
        {
            tokenName = funcDef.Name;
        }
        else if (token.Parent is CommandAst CommAst)
        {
            tokenName = CommAst.GetCommandName();
        }
        IterativeFunctionRename visitor = new(
            tokenName,
            renameParams.NewName,
            position.Line,
            position.Column,
            scriptAst
        );
        visitor.Visit(scriptAst);
        return visitor.Modifications.ToArray();
    }

    internal static TextEdit[] RenameVariable(Ast symbol, Ast scriptAst, RenameParams requestParams)
    {
        if (symbol is VariableExpressionAst or ParameterAst or CommandParameterAst or StringConstantExpressionAst)
        {

            IterativeVariableRename visitor = new(
                requestParams.NewName,
                symbol.Extent.StartLineNumber,
                symbol.Extent.StartColumnNumber,
                scriptAst,
                null //FIXME: Pass through Alias config
            );
            visitor.Visit(scriptAst);
            return visitor.Modifications.ToArray();

        }
        return [];
    }
}

public class RenameSymbolOptions
{
    public bool CreateAlias { get; set; }
}

/// <summary>
/// Represents a position in a script file that adapts and implicitly converts based on context. PowerShell script lines/columns start at 1, but LSP textdocument lines/columns start at 0. The default line/column constructor is 1-based.
/// </summary>
public record ScriptPositionAdapter(IScriptPosition position) : IScriptPosition, IComparable<ScriptPositionAdapter>, IComparable<Position>, IComparable<ScriptPosition>
{
    public int Line => position.LineNumber;
    public int Column => position.ColumnNumber;
    public int Character => position.ColumnNumber;
    public int LineNumber => position.LineNumber;
    public int ColumnNumber => position.ColumnNumber;

    public string File => position.File;
    string IScriptPosition.Line => position.Line;
    public int Offset => position.Offset;

    public ScriptPositionAdapter(int Line, int Column) : this(new ScriptPosition(null, Line, Column, null)) { }
    public ScriptPositionAdapter(ScriptPosition position) : this((IScriptPosition)position) { }

    public ScriptPositionAdapter(Position position) : this(position.Line + 1, position.Character + 1) { }
    public static implicit operator ScriptPositionAdapter(Position position) => new(position);
    public static implicit operator Position(ScriptPositionAdapter scriptPosition) => new
    (
        scriptPosition.position.LineNumber - 1, scriptPosition.position.ColumnNumber - 1
    );


    public static implicit operator ScriptPositionAdapter(ScriptPosition position) => new(position);
    public static implicit operator ScriptPosition(ScriptPositionAdapter position) => position;

    internal ScriptPositionAdapter Delta(int LineAdjust, int ColumnAdjust) => new(
        position.LineNumber + LineAdjust,
        position.ColumnNumber + ColumnAdjust
    );

    public int CompareTo(ScriptPositionAdapter other)
    {
        if (position.LineNumber == other.position.LineNumber)
        {
            return position.ColumnNumber.CompareTo(other.position.ColumnNumber);
        }
        return position.LineNumber.CompareTo(other.position.LineNumber);
    }
    public int CompareTo(Position other) => CompareTo((ScriptPositionAdapter)other);
    public int CompareTo(ScriptPosition other) => CompareTo((ScriptPositionAdapter)other);
    public string GetFullScript() => throw new NotImplementedException();
}

/// <summary>
/// Represents a range in a script file that adapts and implicitly converts based on context. PowerShell script lines/columns start at 1, but LSP textdocument lines/columns start at 0. The default ScriptExtent constructor is 1-based
/// </summary>
/// <param name="extent"></param>
internal record ScriptExtentAdapter(IScriptExtent extent) : IScriptExtent
{
    public ScriptPositionAdapter Start = new(extent.StartScriptPosition);
    public ScriptPositionAdapter End = new(extent.EndScriptPosition);

    public static implicit operator ScriptExtentAdapter(ScriptExtent extent) => new(extent);

    public static implicit operator ScriptExtentAdapter(Range range) => new(new ScriptExtent(
        // Will get shifted to 1-based
        new ScriptPositionAdapter(range.Start),
        new ScriptPositionAdapter(range.End)
    ));
    public static implicit operator Range(ScriptExtentAdapter adapter) => new()
    {
        // Will get shifted to 0-based
        Start = adapter.Start,
        End = adapter.End
    };

    public static implicit operator ScriptExtent(ScriptExtentAdapter adapter) => adapter;

    public static implicit operator RangeOrPlaceholderRange(ScriptExtentAdapter adapter) => new((Range)adapter);

    public IScriptPosition StartScriptPosition => Start;
    public IScriptPosition EndScriptPosition => End;
    public int EndColumnNumber => End.ColumnNumber;
    public int EndLineNumber => End.LineNumber;
    public int StartOffset => extent.EndOffset;
    public int EndOffset => extent.EndOffset;
    public string File => extent.File;
    public int StartColumnNumber => extent.StartColumnNumber;
    public int StartLineNumber => extent.StartLineNumber;
    public string Text => extent.Text;

    public bool Contains(Position position) => ContainsPosition(this, position);
    public static bool ContainsPosition(ScriptExtentAdapter range, ScriptPositionAdapter position) => Range.ContainsPosition(range, position);
}
