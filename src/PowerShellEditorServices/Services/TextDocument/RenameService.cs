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

/// <summary>
/// Used with Configuration Bind to sync the settings to what is set on the client.
/// </summary>
public class RenameServiceOptions
{
    public bool createFunctionAlias { get; set; }
    public bool createParameterAlias { get; set; }
    public bool acceptDisclaimer { get; set; }
}

internal interface IRenameService
{
    /// <summary>
    /// Implementation of textDocument/prepareRename
    /// </summary>
    internal Task<RangeOrPlaceholderRange?> PrepareRenameSymbol(PrepareRenameParams prepareRenameParams, CancellationToken cancellationToken);

    /// <summary>
    /// Implementation of textDocument/rename
    /// </summary>
    internal Task<WorkspaceEdit?> RenameSymbol(RenameParams renameParams, CancellationToken cancellationToken);
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
    internal bool DisclaimerAcceptedForSession; //This is exposed to allow testing non-interactively
    private bool DisclaimerDeclinedForSession;
    private const string ConfigSection = "powershell.rename";
    private RenameServiceOptions? options;
    public async Task<RangeOrPlaceholderRange?> PrepareRenameSymbol(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        RenameParams renameRequest = new()
        {
            NewName = "PREPARERENAMETEST", //A placeholder just to gather edits
            Position = request.Position,
            TextDocument = request.TextDocument
        };

        // TODO: As a performance optimization, should we cache these results and just fetch them on the actual rename, and move the bulk to an implementation method? Seems pretty fast right now but may slow down on large documents. Need to add a large document test example.
        WorkspaceEdit? renameResponse = await RenameSymbol(renameRequest, cancellationToken).ConfigureAwait(false);

        // Since LSP 3.16 we can simply basically return a DefaultBehavior true or null to signal to the client that the position is valid for rename and it should use its default selection criteria (which is probably the language semantic highlighting or grammar). For the current scope of the rename provider, this should be fine, but we have the option to supply the specific range in the future for special cases.
        RangeOrPlaceholderRange renameSupported = new(new RenameDefaultBehavior() { DefaultBehavior = true });

        return (renameResponse?.Changes?[request.TextDocument.Uri].ToArray().Length > 0)
            ? renameSupported
            : null;
    }

    public async Task<WorkspaceEdit?> RenameSymbol(RenameParams request, CancellationToken cancellationToken)
    {
        // We want scoped settings because a workspace setting might be relevant here.
        options = await GetScopedSettings(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);

        if (!await AcceptRenameDisclaimer(options.acceptDisclaimer, cancellationToken).ConfigureAwait(false)) { return null; }

        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);
        ScriptPositionAdapter position = request.Position;

        Ast? tokenToRename = FindRenamableSymbol(scriptFile, position);
        if (tokenToRename is null) { return null; }

        // TODO: Potentially future cross-file support
        TextEdit[] changes = tokenToRename switch
        {
            FunctionDefinitionAst
            or CommandAst
            => RenameFunction(tokenToRename, scriptFile.ScriptAst, request),

            VariableExpressionAst
            or CommandParameterAst
            or StringConstantExpressionAst
            => RenameVariable(tokenToRename, scriptFile.ScriptAst, request),

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

    private static TextEdit[] RenameFunction(Ast target, Ast scriptAst, RenameParams renameParams)
    {
        RenameFunctionVisitor visitor = new(target, renameParams.NewName);
        return visitor.VisitAndGetEdits(scriptAst);
    }

    private TextEdit[] RenameVariable(Ast symbol, Ast scriptAst, RenameParams requestParams)
    {
        RenameVariableVisitor visitor = new(
            symbol, requestParams.NewName, createParameterAlias: options?.createParameterAlias ?? false
        );
        return visitor.VisitAndGetEdits(scriptAst);
    }

    /// <summary>
    /// Finds the most specific renamable symbol at the given position
    /// </summary>
    /// <returns>Ast of the token or null if no renamable symbol was found</returns>
    internal static Ast? FindRenamableSymbol(ScriptFile scriptFile, IScriptPosition position)
    {
        List<Type> renameableAstTypes = [
            // Functions
            typeof(FunctionDefinitionAst),
            typeof(CommandAst),

            // Variables
            typeof(VariableExpressionAst),
            typeof(CommandParameterAst),
            typeof(StringConstantExpressionAst)
        ];
        Ast? ast = scriptFile.ScriptAst.FindClosest(position, renameableAstTypes.ToArray());

        if (ast is StringConstantExpressionAst stringAst)
        {
            // Only splat string parameters should be considered for evaluation.
            if (stringAst.FindSplatParameterReference() is not null) { return stringAst; }
            // Otherwise redo the search without stringConstant, so the most specific is a command, etc.
            renameableAstTypes.Remove(typeof(StringConstantExpressionAst));
            ast = scriptFile.ScriptAst.FindClosest(position, renameableAstTypes.ToArray());
        }

        // Performance optimizations

        // Only the function name is valid for rename, not other components
        if (ast is FunctionDefinitionAst funcDefAst)
        {
            if (!funcDefAst.GetFunctionNameExtent().Contains(position))
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
    /// Prompts the user to accept the rename disclaimer.
    /// </summary>
    /// <returns>true if accepted, false if rejected</returns>
    private async Task<bool> AcceptRenameDisclaimer(bool acceptDisclaimerOption, CancellationToken cancellationToken)
    {
        const string disclaimerDeclinedMessage = "PowerShell rename has been disabled for this session as the disclaimer message was declined. Please restart the extension if you wish to use rename and accept the disclaimer.";

        if (DisclaimerDeclinedForSession) { throw new HandlerErrorException(disclaimerDeclinedMessage); }
        if (acceptDisclaimerOption || DisclaimerAcceptedForSession) { return true; }

        // TODO: Localization
        const string renameDisclaimer = "PowerShell rename functionality is only supported in a limited set of circumstances. [Please review the notice](https://github.com/PowerShell/PowerShellEditorServices?tab=readme-ov-file#rename-disclaimer) and accept the limitations and risks.";
        const string acceptAnswer = "I Accept";
        // const string acceptWorkspaceAnswer = "I Accept [Workspace]";
        // const string acceptSessionAnswer = "I Accept [Session]";
        const string declineAnswer = "Decline";

        // TODO: Unfortunately the LSP spec has no spec for the server to change a client setting, so
        // We have a suboptimal experience until we implement a custom feature for this.
        ShowMessageRequestParams reqParams = new()
        {
            Type = MessageType.Warning,
            Message = renameDisclaimer,
            Actions = new MessageActionItem[] {
                new MessageActionItem() { Title = acceptAnswer },
                new MessageActionItem() { Title = declineAnswer }
                // new MessageActionItem() { Title = acceptWorkspaceAnswer },
                // new MessageActionItem() { Title = acceptSessionAnswer },
            }
        };

        MessageActionItem? result = await lsp.SendRequest(reqParams, cancellationToken).ConfigureAwait(false);
        // null happens if the user closes the dialog rather than making a selection.
        if (result is null || result.Title == declineAnswer)
        {
            const string renameDisabledNotice = "PowerShell Rename functionality will be disabled for this session and you will not be prompted again until restart.";

            ShowMessageParams msgParams = new()
            {
                Message = renameDisabledNotice,
                Type = MessageType.Info
            };
            lsp.SendNotification(msgParams);
            DisclaimerDeclinedForSession = true;
            throw new HandlerErrorException(disclaimerDeclinedMessage);
        }
        if (result.Title == acceptAnswer)
        {
            const string acceptDisclaimerNotice = "PowerShell rename functionality has been enabled for this session. To avoid this prompt in the future, set the powershell.rename.acceptDisclaimer to true in your settings.";
            ShowMessageParams msgParams = new()
            {
                Message = acceptDisclaimerNotice,
                Type = MessageType.Info
            };
            lsp.SendNotification(msgParams);

            DisclaimerAcceptedForSession = true;
            return DisclaimerAcceptedForSession;
        }
        // if (result.Title == acceptWorkspaceAnswer)
        // {
        //     // FIXME: Set the appropriate setting
        //     return true;
        // }
        // if (result.Title == acceptSessionAnswer)
        // {
        //     // FIXME: Set the appropriate setting
        //     return true;
        // }

        throw new InvalidOperationException("Unknown Disclaimer Response received. This is a bug and you should report it.");
    }

    private async Task<RenameServiceOptions> GetScopedSettings(DocumentUri uri, CancellationToken cancellationToken = default)
    {
        IScopedConfiguration scopedConfig = await config.GetScopedConfiguration(uri, cancellationToken).ConfigureAwait(false);
        return scopedConfig.GetSection(ConfigSection).Get<RenameServiceOptions>() ?? new RenameServiceOptions();
    }
}

internal abstract class RenameVisitorBase : AstVisitor
{
    internal List<TextEdit> Edits { get; } = new();
    internal Ast? CurrentDocument { get; set; }

    /// <summary>
    /// A convenience method to get text edits from a specified AST.
    /// </summary>
    internal virtual TextEdit[] VisitAndGetEdits(Ast ast)
    {
        ast.Visit(this);
        return Edits.ToArray();
    }
}

/// <summary>
/// A visitor that generates a list of TextEdits to a TextDocument to rename a PowerShell function
/// You should use a new instance for each rename operation.
/// Skipverify can be used as a performance optimization when you are sure you are in scope.
/// </summary>
internal class RenameFunctionVisitor(Ast target, string newName, bool skipVerify = false) : RenameVisitorBase
{
    private FunctionDefinitionAst? FunctionToRename;

    // Wire up our visitor to the relevant AST types we are potentially renaming
    public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst ast) => Visit(ast);
    public override AstVisitAction VisitCommand(CommandAst ast) => Visit(ast);

    internal AstVisitAction Visit(Ast ast)
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
                    ?? throw new HandlerErrorException("The command to rename does not have a function definition. Renaming a function is only supported when the function is defined within an accessible scope"),
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

    internal bool ShouldRename(Ast candidate)
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

            if (!IsValidFunctionName(newName))
            {
                throw new HandlerErrorException($"{newName} is not a valid function name.");
            }

            ScriptExtentAdapter functionNameExtent = funcDef.GetFunctionNameExtent();

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
            throw new InvalidOperationException("Command element should always have a string expression as its first item. This is a bug and you should report it.");
        }

        return new TextEdit()
        {
            NewText = newName,
            Range = new ScriptExtentAdapter(funcName.Extent)
        };
    }

    internal static bool IsValidFunctionName(string name)
    {
        // Allows us to supply function:varname or varname and get a proper result
        string candidate = "function " + name.TrimStart('$').TrimStart('-') + " {}";
        Ast ast = Parser.ParseInput(candidate, out _, out ParseError[] errors);
        if (errors.Length > 0)
        {
            return false;
        }

        return (ast.Find(a => a is FunctionDefinitionAst, false) as FunctionDefinitionAst)?
            .Name is not null;
    }
}

internal class RenameVariableVisitor(Ast target, string newName, bool skipVerify = false, bool createParameterAlias = false) : RenameVisitorBase
{
    // Used to store the original definition of the variable to use as a reference.
    internal Ast? VariableDefinition;

    // Validate and cleanup the newName definition. User may have left off the $
    // TODO: Full AST parsing to validate the name
    private readonly string NewName = newName.TrimStart('$').TrimStart('-');

    // Wire up our visitor to the relevant AST types we are potentially renaming
    public override AstVisitAction VisitVariableExpression(VariableExpressionAst ast) => Visit(ast);
    public override AstVisitAction VisitCommandParameter(CommandParameterAst ast) => Visit(ast);
    public override AstVisitAction VisitStringConstantExpression(StringConstantExpressionAst ast) => Visit(ast);

    internal AstVisitAction Visit(Ast ast)
    {
        // If this is our first visit, we need to initialize and verify the scope, otherwise verify we are still on the same document.
        if (!skipVerify && CurrentDocument is null || VariableDefinition is null)
        {
            CurrentDocument = ast.GetHighestParent();
            if (CurrentDocument.Find(ast => ast == target, true) is null)
            {
                throw new TargetSymbolNotFoundException("The target this visitor would rename is not present in the AST. This is a bug and you should file an issue");
            }

            // Get the original assignment of our variable, this makes finding rename targets easier in subsequent visits as well as allows us to short-circuit quickly.
            VariableDefinition = target.GetTopVariableAssignment();
            if (VariableDefinition is null)
            {
                throw new HandlerErrorException("The variable element to rename does not have a definition. Renaming an element is only supported when the variable element is defined within an accessible scope");
            }
        }
        else if (CurrentDocument != ast.GetHighestParent())
        {
            throw new TargetSymbolNotFoundException("The visitor should not be reused to rename a different document. It should be created new for each rename operation. This is a bug and you should file an issue");
        }

        if (ShouldRename(ast))
        {
            if (
                createParameterAlias
                && ast == VariableDefinition
                && VariableDefinition is not null and VariableExpressionAst varDefAst
                && varDefAst.Parent is ParameterAst paramAst
            )
            {
                Edits.Add(new TextEdit
                {
                    NewText = $"[Alias('{varDefAst.VariablePath.UserPath}')]",
                    Range = new Range()
                    {
                        Start = new ScriptPositionAdapter(paramAst.Extent.StartScriptPosition),
                        End = new ScriptPositionAdapter(paramAst.Extent.StartScriptPosition)
                    }
                });
            }

            Edits.Add(GetRenameVariableEdit(ast));
        }

        return AstVisitAction.Continue;
    }

    private bool ShouldRename(Ast candidate)
    {
        if (VariableDefinition is null)
        {
            throw new InvalidOperationException("VariableDefinition should always be set by now from first Visit. This is a bug and you should file an issue.");
        }

        if (candidate == VariableDefinition) { return true; }
        // Performance optimization
        if (VariableDefinition.IsAfter(candidate)) { return false; }

        if (candidate.GetTopVariableAssignment() == VariableDefinition) { return true; }

        return false;
    }

    private TextEdit GetRenameVariableEdit(Ast ast)
    {
        return ast switch
        {
            VariableExpressionAst var => !IsValidVariableName(NewName)
                ? throw new HandlerErrorException($"${NewName} is not a valid variable name.")
                : new TextEdit
                {
                    NewText = '$' + NewName,
                    Range = new ScriptExtentAdapter(var.Extent)
                },
            StringConstantExpressionAst stringAst => !IsValidVariableName(NewName)
                ? throw new Exception($"{NewName} is not a valid variable name.")
                : new TextEdit
                {
                    NewText = NewName,
                    Range = new ScriptExtentAdapter(stringAst.Extent)
                },
            CommandParameterAst param => !IsValidCommandParameterName(NewName)
                ? throw new Exception($"-{NewName} is not a valid command parameter name.")
                : new TextEdit
                {
                    NewText = '-' + NewName,
                    Range = new ScriptExtentAdapter(param.Extent)
                },
            _ => throw new InvalidOperationException($"GetRenameVariableEdit was called on an Ast that was not the target. This is a bug and you should file an issue.")
        };
    }

    internal static bool IsValidVariableName(string name)
    {
        // Allows us to supply $varname or varname and get a proper result
        string candidate = '$' + name.TrimStart('$').TrimStart('-');
        Parser.ParseInput(candidate, out Token[] tokens, out _);
        return tokens.Length is 2
            && tokens[0].Kind == TokenKind.Variable
            && tokens[1].Kind == TokenKind.EndOfInput;
    }

    internal static bool IsValidCommandParameterName(string name)
    {
        // Allows us to supply -varname or varname and get a proper result
        string candidate = "Command -" + name.TrimStart('$').TrimStart('-');
        Parser.ParseInput(candidate, out Token[] tokens, out _);
        return tokens.Length == 3
            && tokens[0].Kind == TokenKind.Command
            && tokens[1].Kind == TokenKind.Parameter
            && tokens[2].Kind == TokenKind.EndOfInput;
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
}

/// <summary>
/// Represents a range in a script file that adapts and implicitly converts based on context. PowerShell script lines/columns start at 1, but LSP textdocument lines/columns start at 0. The default ScriptExtent constructor is 1-based
/// </summary>
/// <param name="extent"></param>
internal record ScriptExtentAdapter(IScriptExtent extent) : IScriptExtent
{
    internal ScriptPositionAdapter Start = new(extent.StartScriptPosition);
    internal ScriptPositionAdapter End = new(extent.EndScriptPosition);

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
}
