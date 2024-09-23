// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
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
    public async Task<RangeOrPlaceholderRange?> PrepareRenameSymbol(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        // FIXME: Config actually needs to be read and implemented, this is to make the referencing satisfied
        // config.ToString();
        // ShowMessageRequestParams reqParams = new()
        // {
        //     Type = MessageType.Warning,
        //     Message = "Test Send",
        //     Actions = new MessageActionItem[] {
        //         new MessageActionItem() { Title = "I Accept" },
        //         new MessageActionItem() { Title = "I Accept [Workspace]" },
        //         new MessageActionItem() { Title = "Decline" }
        //     }
        // };

        // MessageActionItem result = await lsp.SendRequest(reqParams, cancellationToken).ConfigureAwait(false);
        // if (result.Title == "Test Action")
        // {
        //     // FIXME: Need to accept
        //     Console.WriteLine("yay");
        // }

        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);

        // TODO: Is this too aggressive? We can still rename inside a var/function even if dotsourcing is in use in a file, we just need to be clear it's not supported to take rename actions inside the dotsourced file.
        if (Utilities.AssertContainsDotSourced(scriptFile.ScriptAst))
        {
            throw new HandlerErrorException("Dot Source detected, this is currently not supported");
        }

        ScriptPositionAdapter position = request.Position;
        Ast? target = FindRenamableSymbol(scriptFile, position);

        // Since 3.16 we can simply basically return a DefaultBehavior true or null to signal to the client that the position is valid for rename and it should use its default selection criteria (which is probably the language semantic highlighting or grammar). For the current scope of the rename provider, this should be fine, but we have the option to supply the specific range in the future for special cases.
        RangeOrPlaceholderRange? renamable = target is null ? null : new RangeOrPlaceholderRange
        (
            new RenameDefaultBehavior() { DefaultBehavior = true }
        );
        return renamable;
    }

    public async Task<WorkspaceEdit?> RenameSymbol(RenameParams request, CancellationToken cancellationToken)
    {

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

    internal static TextEdit[] RenameFunction(Ast target, Ast scriptAst, RenameParams renameParams)
    {
        if (target is not FunctionDefinitionAst or CommandAst)
        {
            throw new HandlerErrorException($"Asked to rename a function but the target is not a viable function type: {target.GetType()}. This should not happen as PrepareRename should have already checked for viability. File an issue if you see this.");
        }

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
            typeof(CommandParameterAst),
            typeof(ParameterAst),
            typeof(StringConstantExpressionAst),
            typeof(CommandAst)
        ]);

        // Special detection for Function calls that dont follow verb-noun syntax e.g. DoThing
        // It's not foolproof but should work in most cases where it is explicit (e.g. not & $x)
        if (ast is StringConstantExpressionAst stringAst)
        {
            if (stringAst.Parent is not CommandAst parent) { return null; }
            if (parent.GetCommandName() != stringAst.Value) { return null; }
            if (parent.CommandElements[0] != stringAst) { return null; }
            // TODO: Potentially find if function was defined earlier in the file to avoid native executable renames and whatnot?
        }

        // Only the function name is valid for rename, not other components
        if (ast is FunctionDefinitionAst funcDefAst)
        {
            if (!GetFunctionNameExtent(funcDefAst).Contains(position))
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
}

/// <summary>
/// A visitor that renames a function given a particular target. The Edits property contains the edits when complete.
/// You should use a new instance for each rename operation.
/// Skipverify can be used as a performance optimization when you are sure you are in scope.
/// </summary>
/// <param name="target"></param>
public class RenameFunctionVisitor(Ast target, string oldName, string newName, bool skipVerify = false) : AstVisitor
{
    public List<TextEdit> Edits { get; } = new();
    private Ast? CurrentDocument;

    // Wire up our visitor to the relevant AST types we are potentially renaming
    public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst ast) => Visit(ast);
    public override AstVisitAction VisitCommand(CommandAst ast) => Visit(ast);

    public AstVisitAction Visit(Ast ast)
    {
        /// If this is our first run, we need to verify we are in scope.
        if (!skipVerify && CurrentDocument is null)
        {
            if (ast.Find(ast => ast == target, true) is null)
            {
                throw new TargetSymbolNotFoundException("The target this visitor would rename is not present in the AST. This is a bug and you should file an issue");
            }
            CurrentDocument = ast;

            // If our target was a command, we need to find the original function.
            if (target is CommandAst command)
            {
                target = CurrentDocument.GetFunctionDefinition(command)
                    ?? throw new TargetSymbolNotFoundException("The command to rename does not have a function definition.");
            }
        }
        if (CurrentDocument != ast)
        {
            throw new TargetSymbolNotFoundException("The visitor should not be reused to rename a different document. It should be created new for each rename operation. This is a bug and you should file an issue");
        }

        if (ShouldRename(ast))
        {
            Edits.Add(GetRenameFunctionEdit(ast));
            return AstVisitAction.Continue;
        }
        else
        {
            return AstVisitAction.SkipChildren;
        }

        /// TODO: Is there a way we can know we are fully outside where the function might be referenced, and if so, call a AstVisitAction Abort as a perf optimization?
    }

    public bool ShouldRename(Ast candidate)
    {
        // There should be only one function definition and if it is not our target, it may be a duplicately named function
        if (candidate is FunctionDefinitionAst funcDef)
        {
            return funcDef == target;
        }

        if (candidate is not CommandAst)
        {
            throw new InvalidOperationException($"ShouldRename for a function had an Unexpected Ast Type {candidate.GetType()}. This is a bug and you should file an issue.");
        }

        // Determine if calls of the function are in the same scope as the function definition
        if (candidate?.Parent?.Parent is ScriptBlockAst)
        {
            return target.Parent.Parent == candidate.Parent.Parent;
        }
        else if (candidate?.Parent is StatementBlockAst)
        {
            return candidate.Parent == target.Parent;
        }

        // If we get this far, we hit an edge case
        throw new InvalidOperationException("ShouldRename for a function could not determine the viability of a rename. This is a bug and you should file an issue.");
    }

    private TextEdit GetRenameFunctionEdit(Ast candidate)
    {
        if (candidate is FunctionDefinitionAst funcDef)
        {
            if (funcDef != target)
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

        if (command.GetCommandName()?.ToLower() == oldName.ToLower() &&
            target.Extent.StartLineNumber <= command.Extent.StartLineNumber)
        {
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

        throw new InvalidOperationException("GetRenameFunctionEdit was not provided a FuncitonDefinition or a CommandAst");
    }
}

public class RenameSymbolOptions
{
    public bool CreateAlias { get; set; }


}


public static class AstExtensions
{
    /// <summary>
    /// Finds the most specific Ast at the given script position, or returns null if none found.<br/>
    /// For example, if the position is on a variable expression within a function definition,
    /// the variable will be returned even if the function definition is found first.
    /// </summary>
    internal static Ast? FindAtPosition(this Ast ast, IScriptPosition position, Type[]? allowedTypes)
    {
        // Short circuit quickly if the position is not in the provided range, no need to traverse if not
        // TODO: Maybe this should be an exception instead? I mean technically its not found but if you gave a position outside the file something very wrong probably happened.
        if (!new ScriptExtentAdapter(ast.Extent).Contains(position)) { return null; }

        // This will be updated with each loop, and re-Find to dig deeper
        Ast? mostSpecificAst = null;
        Ast? currentAst = ast;

        do
        {
            currentAst = currentAst.Find(thisAst =>
            {
                if (thisAst == mostSpecificAst) { return false; }

                int line = position.LineNumber;
                int column = position.ColumnNumber;

                // Performance optimization, skip statements that don't contain the position
                if (
                    thisAst.Extent.EndLineNumber < line
                    || thisAst.Extent.StartLineNumber > line
                    || (thisAst.Extent.EndLineNumber == line && thisAst.Extent.EndColumnNumber < column)
                    || (thisAst.Extent.StartLineNumber == line && thisAst.Extent.StartColumnNumber > column)
                )
                {
                    return false;
                }

                if (allowedTypes is not null && !allowedTypes.Contains(thisAst.GetType()))
                {
                    return false;
                }

                if (new ScriptExtentAdapter(thisAst.Extent).Contains(position))
                {
                    mostSpecificAst = thisAst;
                    return true; //Stops this particular find and looks more specifically
                }

                return false;
            }, true);

            if (currentAst is not null)
            {
                mostSpecificAst = currentAst;
            }
        } while (currentAst is not null);

        return mostSpecificAst;
    }

    public static FunctionDefinitionAst? GetFunctionDefinition(this Ast ast, CommandAst command)
    {
        string? name = command.GetCommandName();
        if (name is null) { return null; }

        List<FunctionDefinitionAst> FunctionDefinitions = ast.FindAll(ast =>
        {
            return ast is FunctionDefinitionAst funcDef &&
            funcDef.Name.ToLower() == name &&
            (funcDef.Extent.EndLineNumber < command.Extent.StartLineNumber ||
            (funcDef.Extent.EndColumnNumber <= command.Extent.StartColumnNumber &&
            funcDef.Extent.EndLineNumber <= command.Extent.StartLineNumber));
        }, true).Cast<FunctionDefinitionAst>().ToList();

        // return the function def if we only have one match
        if (FunctionDefinitions.Count == 1)
        {
            return FunctionDefinitions[0];
        }
        // Determine which function definition is the right one
        FunctionDefinitionAst? CorrectDefinition = null;
        for (int i = FunctionDefinitions.Count - 1; i >= 0; i--)
        {
            FunctionDefinitionAst element = FunctionDefinitions[i];

            Ast parent = element.Parent;
            // walk backwards till we hit a functiondefinition if any
            while (null != parent)
            {
                if (parent is FunctionDefinitionAst)
                {
                    break;
                }
                parent = parent.Parent;
            }
            // we have hit the global scope of the script file
            if (null == parent)
            {
                CorrectDefinition = element;
                break;
            }

            if (command?.Parent == parent)
            {
                CorrectDefinition = (FunctionDefinitionAst)parent;
            }
        }
        return CorrectDefinition;
    }
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

    public static FunctionDefinitionAst? GetFunctionDefByCommandAst(string OldName, int StartLineNumber, int StartColumnNumber, Ast ScriptFile)
    {
        // Look up the targeted object
        CommandAst? TargetCommand = (CommandAst?)GetAstAtPositionOfType(StartLineNumber, StartColumnNumber, ScriptFile
        , typeof(CommandAst));

        if (TargetCommand?.GetCommandName().ToLower() != OldName.ToLower())
        {
            TargetCommand = null;
        }

        string? FunctionName = TargetCommand?.GetCommandName();

        List<FunctionDefinitionAst> FunctionDefinitions = ScriptFile.FindAll(ast =>
        {
            return ast is FunctionDefinitionAst FuncDef &&
            FuncDef.Name.ToLower() == OldName.ToLower() &&
            (FuncDef.Extent.EndLineNumber < TargetCommand?.Extent.StartLineNumber ||
            (FuncDef.Extent.EndColumnNumber <= TargetCommand?.Extent.StartColumnNumber &&
            FuncDef.Extent.EndLineNumber <= TargetCommand.Extent.StartLineNumber));
        }, true).Cast<FunctionDefinitionAst>().ToList();
        // return the function def if we only have one match
        if (FunctionDefinitions.Count == 1)
        {
            return FunctionDefinitions[0];
        }
        // Determine which function definition is the right one
        FunctionDefinitionAst? CorrectDefinition = null;
        for (int i = FunctionDefinitions.Count - 1; i >= 0; i--)
        {
            FunctionDefinitionAst element = FunctionDefinitions[i];

            Ast parent = element.Parent;
            // walk backwards till we hit a functiondefinition if any
            while (null != parent)
            {
                if (parent is FunctionDefinitionAst)
                {
                    break;
                }
                parent = parent.Parent;
            }
            // we have hit the global scope of the script file
            if (null == parent)
            {
                CorrectDefinition = element;
                break;
            }

            if (TargetCommand?.Parent == parent)
            {
                CorrectDefinition = (FunctionDefinitionAst)parent;
            }
        }
        return CorrectDefinition;
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
