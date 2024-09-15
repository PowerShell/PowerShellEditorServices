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
using OmniSharp.Extensions.LanguageServer.Protocol;
using System;
using PowerShellEditorServices.Services.PowerShell.Utility;

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

        ScriptPositionAdapter position = request.Position;
        Ast token = FindRenamableSymbol(scriptFile, position);
        if (token is null) { return null; }

        // TODO: Really should have a class with implicit convertors handing these conversions to avoid off-by-one mistakes.
        return Utilities.ToRange(token.Extent); ;
    }

    /// <summary>
    /// Finds a renamable symbol at a given position in a script file using 1-based row/column references
    /// <param name="scriptFile"/>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// </summary>
    internal static Ast FindRenamableSymbol(ScriptFile scriptFile, int line, int column) =>
        FindRenamableSymbol(scriptFile, new ScriptPositionAdapter(line, column));

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

            return ast.Extent.Contains(position);
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

    public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
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
            return visitor.Modifications.ToArray();

        }
        return null;
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
    public ScriptPositionAdapter(Position position) : this(position.Line + 1, position.Character + 1) { }

    public static implicit operator ScriptPositionAdapter(Position position) => new(position);
    public static implicit operator ScriptPositionAdapter(ScriptPosition position) => new(position);

    public static implicit operator Position(ScriptPositionAdapter scriptPosition) => new(scriptPosition.position.LineNumber - 1, scriptPosition.position.ColumnNumber - 1);
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
    public readonly ScriptPositionAdapter Start = new(extent.StartScriptPosition);
    public readonly ScriptPositionAdapter End = new(extent.StartScriptPosition);

    public static implicit operator ScriptExtentAdapter(ScriptExtent extent) => new(extent);
    public static implicit operator ScriptExtent(ScriptExtentAdapter extent) => extent;

    public static implicit operator Range(ScriptExtentAdapter extent) => new()
    {
        // Will get shifted to 0-based
        Start = extent.Start,
        End = extent.End
    };
    public static implicit operator ScriptExtentAdapter(Range range) => new(new ScriptExtent(
        // Will get shifted to 1-based
        new ScriptPositionAdapter(range.Start),
        new ScriptPositionAdapter(range.End)
    ));

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

public class RenameSymbolParams : IRequest<RenameSymbolResult>
{
    public string FileName { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string RenameTo { get; set; }
    public RenameSymbolOptions Options { get; set; }
}

public class RenameSymbolResult
{
    public RenameSymbolResult() => Changes = new List<TextEdit>();
    public List<TextEdit> Changes { get; set; }
}
