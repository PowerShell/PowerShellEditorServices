// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Refactoring;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services;

public interface IRenameService
{
    /// <summary>
    /// Implementation of textDocument/prepareRename
    /// </summary>
    public Task<RangeOrPlaceholderRange?> PrepareRenameSymbol(PrepareRenameParams prepareRenameParams, CancellationToken cancellationToken);

    /// <summary>
    /// Implementation of textDocument/rename
    /// </summary>
    public Task<WorkspaceEdit?> RenameSymbol(RenameParams renameParams, CancellationToken cancellationToken);
}

/// <summary>
/// Providers service for renaming supported symbols such as functions and variables.
/// </summary>
internal class RenameService(
    WorkspaceService workspaceService,
    ILanguageServerFacade lsp,
    ILanguageServerConfiguration config
) : IRenameService
{
    private bool disclaimerDeclined;

    public async Task<RangeOrPlaceholderRange?> PrepareRenameSymbol(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        if (!await AcceptRenameDisclaimer(cancellationToken).ConfigureAwait(false)) { return null; }

        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);

        // TODO: Is this too aggressive? We can still rename inside a var/function even if dotsourcing is in use in a file, we just need to be clear it's not supported to expect rename actions to propogate.
        if (Utilities.AssertContainsDotSourced(scriptFile.ScriptAst))
        {
            throw new HandlerErrorException("Dot Source detected, this is currently not supported");
        }

        // TODO: FindRenamableSymbol may create false positives for renaming, so we probably should go ahead and execute a full rename and return true if edits are found.

        ScriptPositionAdapter position = request.Position;
        Ast? target = FindRenamableSymbol(scriptFile, position);

        // Since LSP 3.16 we can simply basically return a DefaultBehavior true or null to signal to the client that the position is valid for rename and it should use its default selection criteria (which is probably the language semantic highlighting or grammar). For the current scope of the rename provider, this should be fine, but we have the option to supply the specific range in the future for special cases.
        RangeOrPlaceholderRange? renamable = target is null ? null : new RangeOrPlaceholderRange
        (
            new RenameDefaultBehavior() { DefaultBehavior = true }
        );
        return renamable;
    }

    public async Task<WorkspaceEdit?> RenameSymbol(RenameParams request, CancellationToken cancellationToken)
    {
        if (!await AcceptRenameDisclaimer(cancellationToken).ConfigureAwait(false)) { return null; }

        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);
        ScriptPositionAdapter position = request.Position;

        Ast? tokenToRename = FindRenamableSymbol(scriptFile, position);
        if (tokenToRename is null) { return null; }

        // TODO: Potentially future cross-file support
        TextEdit[] changes = tokenToRename switch
        {
            FunctionDefinitionAst or CommandAst => RenameFunction(tokenToRename, scriptFile.ScriptAst, request),
            VariableExpressionAst => RenameVariable(tokenToRename, scriptFile.ScriptAst, request),
            // FIXME: Only throw if capability is not prepareprovider
            _ => throw new InvalidOperationException("This should not happen as PrepareRename should have already checked for viability. File an issue if you see this.")
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

    internal static TextEdit[] RenameFunction(Ast target, Ast scriptAst, RenameParams renameParams)
    {
        if (target is not (FunctionDefinitionAst or CommandAst))
        {
            throw new HandlerErrorException($"Asked to rename a function but the target is not a viable function type: {target.GetType()}. This should not happen as PrepareRename should have already checked for viability. File an issue if you see this.");
        }

        RenameFunctionVisitor visitor = new(target, renameParams.NewName);
        return visitor.VisitAndGetEdits(scriptAst);
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

    /// <summary>
    /// Finds the most specific renamable symbol at the given position
    /// </summary>
    /// <returns>Ast of the token or null if no renamable symbol was found</returns>
    internal static Ast? FindRenamableSymbol(ScriptFile scriptFile, ScriptPositionAdapter position)
    {
        Ast? ast = scriptFile.ScriptAst.FindAtPosition(position,
        [
            // Filters just the ASTs that are candidates for rename
            typeof(FunctionDefinitionAst),
            typeof(VariableExpressionAst),
            typeof(CommandAst)
        ]);

        // Only the function name is valid for rename, not other components
        if (ast is FunctionDefinitionAst funcDefAst)
        {
            if (!GetFunctionNameExtent(funcDefAst).Contains(position))
            {
                return null;
            }
        }

        // Only the command name (function call) portion is renamable
        if (ast is CommandAst command)
        {
            if (command.CommandElements[0] is not StringConstantExpressionAst name)
            {
                return null;
            }

            if (!new ScriptExtentAdapter(name.Extent).Contains(position))
            {
                return null;
            }
        }

        return ast;
    }


    /// <summary>
    /// Return an extent that only contains the position of the name of the function, for Client highlighting purposes.
    /// </summary>
    public static ScriptExtentAdapter GetFunctionNameExtent(FunctionDefinitionAst ast)
    {
        string name = ast.Name;
        // FIXME: Gather dynamically from the AST and include backticks and whatnot that might be present
        int funcLength = "function ".Length;
        ScriptExtentAdapter funcExtent = new(ast.Extent);
        funcExtent.Start = funcExtent.Start.Delta(0, funcLength);
        funcExtent.End = funcExtent.Start.Delta(0, name.Length);

        return funcExtent;
    }

    /// <summary>
    /// Prompts the user to accept the rename disclaimer.
    /// </summary>
    /// <returns>true if accepted, false if rejected</returns>
    private async Task<bool> AcceptRenameDisclaimer(CancellationToken cancellationToken)
    {
        // User has declined for the session so we don't want this popping up a bunch.
        if (disclaimerDeclined) { return false; }

        // FIXME: This should be referencing an options type that is initialized with the Service or is a getter.
        if (config.GetSection("powershell").GetValue<bool>("acceptRenameDisclaimer")) { return true; }

        // TODO: Localization
        const string acceptAnswer = "I Accept";
        const string acceptWorkspaceAnswer = "I Accept [Workspace]";
        const string acceptSessionAnswer = "I Accept [Session]";
        const string declineAnswer = "Decline";
        ShowMessageRequestParams reqParams = new()
        {
            Type = MessageType.Warning,
            Message = "Test Send",
            Actions = new MessageActionItem[] {
                new MessageActionItem() { Title = acceptAnswer },
                new MessageActionItem() { Title = acceptWorkspaceAnswer },
                new MessageActionItem() { Title = acceptSessionAnswer },
                new MessageActionItem() { Title = declineAnswer }
            }
        };

        MessageActionItem result = await lsp.SendRequest(reqParams, cancellationToken).ConfigureAwait(false);
        if (result.Title == declineAnswer)
        {
            ShowMessageParams msgParams = new()
            {
                Message = "PowerShell Rename functionality will be disabled for this session and you will not be prompted again until restart.",
                Type = MessageType.Info
            };
            lsp.SendNotification(msgParams);
            disclaimerDeclined = true;
            return !disclaimerDeclined;
        }
        if (result.Title == acceptAnswer)
        {
            // FIXME: Set the appropriate setting
            return true;
        }
        if (result.Title == acceptWorkspaceAnswer)
        {
            // FIXME: Set the appropriate setting
            return true;
        }
        if (result.Title == acceptSessionAnswer)
        {
            // FIXME: Set the appropriate setting
            return true;
        }

        throw new InvalidOperationException("Unknown Disclaimer Response received. This is a bug and you should report it.");
    }
}

/// <summary>
/// A visitor that generates a list of TextEdits to a TextDocument to rename a PowerShell function
/// You should use a new instance for each rename operation.
/// Skipverify can be used as a performance optimization when you are sure you are in scope.
/// </summary>
public class RenameFunctionVisitor(Ast target, string newName, bool skipVerify = false) : AstVisitor
{
    public List<TextEdit> Edits { get; } = new();
    private Ast? CurrentDocument;
    private FunctionDefinitionAst? FunctionToRename;

    // Wire up our visitor to the relevant AST types we are potentially renaming
    public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst ast) => Visit(ast);
    public override AstVisitAction VisitCommand(CommandAst ast) => Visit(ast);

    public AstVisitAction Visit(Ast ast)
    {
        // If this is our first run, we need to verify we are in scope and gather our rename operation info
        if (!skipVerify && CurrentDocument is null)
        {
            CurrentDocument = ast.GetHighestParent();
            if (CurrentDocument.Find(ast => ast == target, true) is null)
            {
                throw new TargetSymbolNotFoundException("The target this visitor would rename is not present in the AST. This is a bug and you should file an issue");
            }

            FunctionToRename = target switch
            {
                FunctionDefinitionAst f => f,
                CommandAst command => CurrentDocument.FindFunctionDefinition(command)
                    ?? throw new TargetSymbolNotFoundException("The command to rename does not have a function definition. Renaming a function is only supported when the function is defined within the same scope"),
                _ => throw new Exception($"Unsupported AST type {target.GetType()} encountered")
            };
        };

        if (CurrentDocument != ast.GetHighestParent())
        {
            throw new TargetSymbolNotFoundException("The visitor should not be reused to rename a different document. It should be created new for each rename operation. This is a bug and you should file an issue");
        }

        if (ShouldRename(ast))
        {
            Edits.Add(GetRenameFunctionEdit(ast));
        }
        return AstVisitAction.Continue;

        // TODO: Is there a way we can know we are fully outside where the function might be referenced, and if so, call a AstVisitAction Abort as a perf optimization?
    }

    private bool ShouldRename(Ast candidate)
    {
        // Rename our original function definition. There may be duplicate definitions of the same name
        if (candidate is FunctionDefinitionAst funcDef)
        {
            return funcDef == FunctionToRename;
        }

        // Should only be CommandAst (function calls) from this point forward in the visit.
        if (candidate is not CommandAst command)
        {
            throw new InvalidOperationException($"ShouldRename for a function had an Unexpected Ast Type {candidate.GetType()}. This is a bug and you should file an issue.");
        }

        if (CurrentDocument is null)
        {
            throw new InvalidOperationException("CurrentDoc should always be set by now from first Visit. This is a bug and you should file an issue.");
        }

        // Match up the command to its function definition
        return CurrentDocument.FindFunctionDefinition(command) == FunctionToRename;
    }

    private TextEdit GetRenameFunctionEdit(Ast candidate)
    {
        if (candidate is FunctionDefinitionAst funcDef)
        {
            if (funcDef != FunctionToRename)
            {
                throw new InvalidOperationException("GetRenameFunctionEdit was called on an Ast that was not the target. This is a bug and you should file an issue.");
            }

            ScriptExtentAdapter functionNameExtent = RenameService.GetFunctionNameExtent(funcDef);

            return new TextEdit()
            {
                NewText = newName,
                Range = functionNameExtent
            };
        }

        // Should be CommandAst past this point.
        if (candidate is not CommandAst command)
        {
            throw new InvalidOperationException($"Expected a command but got {candidate.GetType()}");
        }

        if (command.CommandElements[0] is not StringConstantExpressionAst funcName)
        {
            throw new InvalidOperationException("Command element should always have a string expresssion as its first item. This is a bug and you should report it.");
        }

        return new TextEdit()
        {
            NewText = newName,
            Range = new ScriptExtentAdapter(funcName.Extent)
        };
    }

    public TextEdit[] VisitAndGetEdits(Ast ast)
    {
        ast.Visit(this);
        return Edits.ToArray();
    }
}

public class RenameSymbolOptions
{
    public bool CreateAlias { get; set; }
}

internal class Utilities
{
    public static Ast? GetAstAtPositionOfType(int StartLineNumber, int StartColumnNumber, Ast ScriptAst, params Type[] type)
    {
        Ast? result = null;
        result = ScriptAst.Find(ast =>
        {
            return ast.Extent.StartLineNumber == StartLineNumber &&
            ast.Extent.StartColumnNumber == StartColumnNumber &&
            type.Contains(ast.GetType());
        }, true);
        if (result == null)
        {
            throw new TargetSymbolNotFoundException();
        }
        return result;
    }

    public static Ast? GetAstParentOfType(Ast ast, params Type[] type)
    {
        Ast parent = ast;
        // walk backwards till we hit a parent of the specified type or return null
        while (null != parent)
        {
            if (type.Contains(parent.GetType()))
            {
                return parent;
            }
            parent = parent.Parent;
        }
        return null;
    }

    public static bool AssertContainsDotSourced(Ast ScriptAst)
    {
        Ast dotsourced = ScriptAst.Find(ast =>
        {
            return ast is CommandAst commandAst && commandAst.InvocationOperator == TokenKind.Dot;
        }, true);
        if (dotsourced != null)
        {
            return true;
        }
        return false;
    }

    public static Ast? GetAst(int StartLineNumber, int StartColumnNumber, Ast Ast)
    {
        Ast? token = null;

        token = Ast.Find(ast =>
        {
            return StartLineNumber == ast.Extent.StartLineNumber &&
            ast.Extent.EndColumnNumber >= StartColumnNumber &&
                StartColumnNumber >= ast.Extent.StartColumnNumber;
        }, true);

        if (token is NamedBlockAst)
        {
            // NamedBlockAST starts on the same line as potentially another AST,
            // its likley a user is not after the NamedBlockAst but what it contains
            IEnumerable<Ast> stacked_tokens = token.FindAll(ast =>
            {
                return StartLineNumber == ast.Extent.StartLineNumber &&
                ast.Extent.EndColumnNumber >= StartColumnNumber
                && StartColumnNumber >= ast.Extent.StartColumnNumber;
            }, true);

            if (stacked_tokens.Count() > 1)
            {
                return stacked_tokens.LastOrDefault();
            }

            return token.Parent;
        }

        if (null == token)
        {
            IEnumerable<Ast> LineT = Ast.FindAll(ast =>
            {
                return StartLineNumber == ast.Extent.StartLineNumber &&
                StartColumnNumber >= ast.Extent.StartColumnNumber;
            }, true);
            return LineT.OfType<FunctionDefinitionAst>()?.LastOrDefault();
        }

        IEnumerable<Ast> tokens = token.FindAll(ast =>
        {
            return ast.Extent.EndColumnNumber >= StartColumnNumber
            && StartColumnNumber >= ast.Extent.StartColumnNumber;
        }, true);
        if (tokens.Count() > 1)
        {
            token = tokens.LastOrDefault();
        }
        return token;
    }
}


/// <summary>
/// Represents a position in a script file that adapts and implicitly converts based on context. PowerShell script lines/columns start at 1, but LSP textdocument lines/columns start at 0. The default line/column constructor is 1-based.
/// </summary>
public record ScriptPositionAdapter(IScriptPosition position) : IScriptPosition, IComparable<ScriptPositionAdapter>, IComparable<Position>, IComparable<ScriptPosition>
{
    public int Line => position.LineNumber;
    public int LineNumber => position.LineNumber;
    public int Column => position.ColumnNumber;
    public int ColumnNumber => position.ColumnNumber;
    public int Character => position.ColumnNumber;

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

    public static implicit operator RangeOrPlaceholderRange(ScriptExtentAdapter adapter) => new((Range)adapter)
    {
        DefaultBehavior = new() { DefaultBehavior = false }
    };

    public IScriptPosition StartScriptPosition => Start;
    public IScriptPosition EndScriptPosition => End;
    public int EndColumnNumber => End.ColumnNumber;
    public int EndLineNumber => End.LineNumber;
    public int StartOffset => extent.StartOffset;
    public int EndOffset => extent.EndOffset;
    public string File => extent.File;
    public int StartColumnNumber => extent.StartColumnNumber;
    public int StartLineNumber => extent.StartLineNumber;
    public string Text => extent.Text;

    public bool Contains(IScriptPosition position) => Contains(new ScriptPositionAdapter(position));

    public bool Contains(ScriptPositionAdapter position)
    {
        if (position.Line < Start.Line || position.Line > End.Line)
        {
            return false;
        }

        if (position.Line == Start.Line && position.Character < Start.Character)
        {
            return false;
        }

        if (position.Line == End.Line && position.Character > End.Character)
        {
            return false;
        }

        return true;
    }
}
