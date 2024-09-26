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
    public bool createVariableAlias { get; set; }
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
    ILanguageServerConfiguration config,
    bool disclaimerDeclinedForSession = false,
    bool disclaimerAcceptedForSession = false,
    string configSection = "powershell.rename"
) : IRenameService
{

    public async Task<RangeOrPlaceholderRange?> PrepareRenameSymbol(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        RenameParams renameRequest = new()
        {
            NewName = "PREPARERENAMETEST", //A placeholder just to gather edits
            Position = request.Position,
            TextDocument = request.TextDocument
        };
        // TODO: Should we cache these resuls and just fetch them on the actual rename, and move the bulk to an implementation method?
        WorkspaceEdit? renameResponse = await RenameSymbol(renameRequest, cancellationToken).ConfigureAwait(false);

        // Since LSP 3.16 we can simply basically return a DefaultBehavior true or null to signal to the client that the position is valid for rename and it should use its default selection criteria (which is probably the language semantic highlighting or grammar). For the current scope of the rename provider, this should be fine, but we have the option to supply the specific range in the future for special cases.
        return (renameResponse?.Changes?[request.TextDocument.Uri].ToArray().Length > 0)
            ? new RangeOrPlaceholderRange
            (
                new RenameDefaultBehavior() { DefaultBehavior = true }
            )
            : null;
    }

    public async Task<WorkspaceEdit?> RenameSymbol(RenameParams request, CancellationToken cancellationToken)
    {
        // We want scoped settings because a workspace setting might be relevant here.
        RenameServiceOptions options = await GetScopedSettings(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);

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
            or ParameterAst
            or CommandParameterAst
            or AssignmentStatementAst
            => RenameVariable(tokenToRename, scriptFile.ScriptAst, request, options.createVariableAlias),

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
            throw new HandlerErrorException($"Asked to rename a function but the target is not a viable function type: {target.GetType()}. This is a bug, file an issue if you see this.");
        }

        RenameFunctionVisitor visitor = new(target, renameParams.NewName);
        return visitor.VisitAndGetEdits(scriptAst);
    }

    internal static TextEdit[] RenameVariable(Ast symbol, Ast scriptAst, RenameParams requestParams, bool createAlias)
    {
        if (symbol is not (VariableExpressionAst or ParameterAst or CommandParameterAst or StringConstantExpressionAst))
        {
            throw new HandlerErrorException($"Asked to rename a variable but the target is not a viable variable type: {symbol.GetType()}. This is a bug, file an issue if you see this.");
        }

        RenameVariableVisitor visitor = new(
            requestParams.NewName,
            symbol.Extent.StartLineNumber,
            symbol.Extent.StartColumnNumber,
            scriptAst,
            createAlias
        );
        return visitor.VisitAndGetEdits();

    }

    /// <summary>
    /// Finds the most specific renamable symbol at the given position
    /// </summary>
    /// <returns>Ast of the token or null if no renamable symbol was found</returns>
    internal static Ast? FindRenamableSymbol(ScriptFile scriptFile, ScriptPositionAdapter position)
    {
        Ast? ast = scriptFile.ScriptAst.FindAtPosition(position,
        [
            // Functions
            typeof(FunctionDefinitionAst),
            typeof(CommandAst),

            // Variables
            typeof(VariableExpressionAst),
            typeof(ParameterAst),
            typeof(CommandParameterAst),
            typeof(AssignmentStatementAst),
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
    internal static ScriptExtentAdapter GetFunctionNameExtent(FunctionDefinitionAst ast)
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
    private async Task<bool> AcceptRenameDisclaimer(bool acceptDisclaimerOption, CancellationToken cancellationToken)
    {
        if (disclaimerDeclinedForSession) { return false; }
        if (acceptDisclaimerOption || disclaimerAcceptedForSession) { return true; }

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

        MessageActionItem result = await lsp.SendRequest(reqParams, cancellationToken).ConfigureAwait(false);
        if (result.Title == declineAnswer)
        {
            const string renameDisabledNotice = "PowerShell Rename functionality will be disabled for this session and you will not be prompted again until restart.";

            ShowMessageParams msgParams = new()
            {
                Message = renameDisabledNotice,
                Type = MessageType.Info
            };
            lsp.SendNotification(msgParams);
            disclaimerDeclinedForSession = true;
            return !disclaimerDeclinedForSession;
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

            disclaimerAcceptedForSession = true;
            return disclaimerAcceptedForSession;
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
        return scopedConfig.GetSection(configSection).Get<RenameServiceOptions>() ?? new RenameServiceOptions();
    }
}

internal abstract class RenameVisitorBase() : AstVisitor
{
    internal List<TextEdit> Edits { get; } = new();
}

/// <summary>
/// A visitor that generates a list of TextEdits to a TextDocument to rename a PowerShell function
/// You should use a new instance for each rename operation.
/// Skipverify can be used as a performance optimization when you are sure you are in scope.
/// </summary>
internal class RenameFunctionVisitor(Ast target, string newName, bool skipVerify = false) : RenameVisitorBase
{
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

    internal TextEdit[] VisitAndGetEdits(Ast ast)
    {
        ast.Visit(this);
        return Edits.ToArray();
    }
}

#nullable disable
internal class RenameVariableVisitor : RenameVisitorBase
{
    private readonly string OldName;
    private readonly string NewName;
    internal bool ShouldRename;
    internal int StartLineNumber;
    internal int StartColumnNumber;
    internal VariableExpressionAst TargetVariableAst;
    internal readonly Ast ScriptAst;
    internal bool isParam;
    internal bool AliasSet;
    internal FunctionDefinitionAst TargetFunction;
    internal bool CreateAlias;

    public RenameVariableVisitor(string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst, bool CreateAlias)
    {
        this.NewName = NewName;
        this.StartLineNumber = StartLineNumber;
        this.StartColumnNumber = StartColumnNumber;
        this.ScriptAst = ScriptAst;
        this.CreateAlias = CreateAlias;

        VariableExpressionAst Node = (VariableExpressionAst)GetVariableTopAssignment(StartLineNumber, StartColumnNumber, ScriptAst);
        if (Node != null)
        {
            if (Node.Parent is ParameterAst)
            {
                isParam = true;
                Ast parent = Node;
                // Look for a target function that the parameterAst will be within if it exists
                parent = Utilities.GetAstParentOfType(parent, typeof(FunctionDefinitionAst));
                if (parent != null)
                {
                    TargetFunction = (FunctionDefinitionAst)parent;
                }
            }
            TargetVariableAst = Node;
            OldName = TargetVariableAst.VariablePath.UserPath.Replace("$", "");
            this.StartColumnNumber = TargetVariableAst.Extent.StartColumnNumber;
            this.StartLineNumber = TargetVariableAst.Extent.StartLineNumber;
        }
    }

    private static Ast GetVariableTopAssignment(int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
    {

        // Look up the target object
        Ast node = Utilities.GetAstAtPositionOfType(StartLineNumber, StartColumnNumber,
        ScriptAst, typeof(VariableExpressionAst), typeof(CommandParameterAst), typeof(StringConstantExpressionAst));

        string name = node switch
        {
            CommandParameterAst commdef => commdef.ParameterName,
            VariableExpressionAst varDef => varDef.VariablePath.UserPath,
            // Key within a Hashtable
            StringConstantExpressionAst strExp => strExp.Value,
            _ => throw new TargetSymbolNotFoundException()
        };

        VariableExpressionAst splatAssignment = null;
        // A rename of a parameter has been initiated from a splat
        if (node is StringConstantExpressionAst)
        {
            Ast parent = node;
            parent = Utilities.GetAstParentOfType(parent, typeof(AssignmentStatementAst));
            if (parent is not null and AssignmentStatementAst assignmentStatementAst)
            {
                splatAssignment = (VariableExpressionAst)assignmentStatementAst.Left.Find(
                    ast => ast is VariableExpressionAst, false);
            }
        }

        Ast TargetParent = GetAstParentScope(node);

        // Is the Variable sitting within a ParameterBlockAst that is within a Function Definition
        // If so we don't need to look further as this is most likley the AssignmentStatement we are looking for
        Ast paramParent = Utilities.GetAstParentOfType(node, typeof(ParamBlockAst));
        if (TargetParent is FunctionDefinitionAst && null != paramParent)
        {
            return node;
        }

        // Find all variables and parameter assignments with the same name before
        // The node found above
        List<VariableExpressionAst> VariableAssignments = ScriptAst.FindAll(ast =>
        {
            return ast is VariableExpressionAst VarDef &&
            VarDef.Parent is AssignmentStatementAst or ParameterAst &&
            VarDef.VariablePath.UserPath.ToLower() == name.ToLower() &&
            // Look Backwards from the node above
            (VarDef.Extent.EndLineNumber < node.Extent.StartLineNumber ||
            (VarDef.Extent.EndColumnNumber <= node.Extent.StartColumnNumber &&
            VarDef.Extent.EndLineNumber <= node.Extent.StartLineNumber));
        }, true).Cast<VariableExpressionAst>().ToList();
        // return the def if we have no matches
        if (VariableAssignments.Count == 0)
        {
            return node;
        }
        Ast CorrectDefinition = null;
        for (int i = VariableAssignments.Count - 1; i >= 0; i--)
        {
            VariableExpressionAst element = VariableAssignments[i];

            Ast parent = GetAstParentScope(element);
            // closest assignment statement is within the scope of the node
            if (TargetParent == parent)
            {
                CorrectDefinition = element;
                break;
            }
            else if (node.Parent is AssignmentStatementAst)
            {
                // the node is probably the first assignment statement within the scope
                CorrectDefinition = node;
                break;
            }
            // node is proably just a reference to an assignment statement or Parameter within the global scope or higher
            if (node.Parent is not AssignmentStatementAst)
            {
                if (null == parent || null == parent.Parent)
                {
                    // we have hit the global scope of the script file
                    CorrectDefinition = element;
                    break;
                }

                if (parent is FunctionDefinitionAst funcDef && node is CommandParameterAst or StringConstantExpressionAst)
                {
                    if (node is StringConstantExpressionAst)
                    {
                        List<VariableExpressionAst> SplatReferences = ScriptAst.FindAll(ast =>
                        {
                            return ast is VariableExpressionAst varDef &&
                            varDef.Splatted &&
                            varDef.Parent is CommandAst &&
                            varDef.VariablePath.UserPath.ToLower() == splatAssignment.VariablePath.UserPath.ToLower();
                        }, true).Cast<VariableExpressionAst>().ToList();

                        if (SplatReferences.Count >= 1)
                        {
                            CommandAst splatFirstRefComm = (CommandAst)SplatReferences.First().Parent;
                            if (funcDef.Name == splatFirstRefComm.GetCommandName()
                            && funcDef.Parent.Parent == TargetParent)
                            {
                                CorrectDefinition = element;
                                break;
                            }
                        }
                    }

                    if (node.Parent is CommandAst commDef)
                    {
                        if (funcDef.Name == commDef.GetCommandName()
                        && funcDef.Parent.Parent == TargetParent)
                        {
                            CorrectDefinition = element;
                            break;
                        }
                    }
                }
                if (WithinTargetsScope(element, node))
                {
                    CorrectDefinition = element;
                }
            }
        }
        return CorrectDefinition ?? node;
    }

    private static Ast GetAstParentScope(Ast node)
    {
        Ast parent = node;
        // Walk backwards up the tree looking for a ScriptBLock of a FunctionDefinition
        parent = Utilities.GetAstParentOfType(parent, typeof(ScriptBlockAst), typeof(FunctionDefinitionAst), typeof(ForEachStatementAst), typeof(ForStatementAst));
        if (parent is ScriptBlockAst && parent.Parent != null && parent.Parent is FunctionDefinitionAst)
        {
            parent = parent.Parent;
        }
        // Check if the parent of the VariableExpressionAst is a ForEachStatementAst then check if the variable names match
        // if so this is probably a variable defined within a foreach loop
        else if (parent is ForEachStatementAst ForEachStmnt && node is VariableExpressionAst VarExp &&
            ForEachStmnt.Variable.VariablePath.UserPath == VarExp.VariablePath.UserPath)
        {
            parent = ForEachStmnt;
        }
        // Check if the parent of the VariableExpressionAst is a ForStatementAst then check if the variable names match
        // if so this is probably a variable defined within a foreach loop
        else if (parent is ForStatementAst ForStmnt && node is VariableExpressionAst ForVarExp &&
                ForStmnt.Initializer is AssignmentStatementAst AssignStmnt && AssignStmnt.Left is VariableExpressionAst VarExpStmnt &&
                VarExpStmnt.VariablePath.UserPath == ForVarExp.VariablePath.UserPath)
        {
            parent = ForStmnt;
        }

        return parent;
    }

    private static bool IsVariableExpressionAssignedInTargetScope(VariableExpressionAst node, Ast scope)
    {
        bool r = false;

        List<VariableExpressionAst> VariableAssignments = node.FindAll(ast =>
        {
            return ast is VariableExpressionAst VarDef &&
            VarDef.Parent is AssignmentStatementAst or ParameterAst &&
            VarDef.VariablePath.UserPath.ToLower() == node.VariablePath.UserPath.ToLower() &&
            // Look Backwards from the node above
            (VarDef.Extent.EndLineNumber < node.Extent.StartLineNumber ||
            (VarDef.Extent.EndColumnNumber <= node.Extent.StartColumnNumber &&
            VarDef.Extent.EndLineNumber <= node.Extent.StartLineNumber)) &&
            // Must be within the the designated scope
            VarDef.Extent.StartLineNumber >= scope.Extent.StartLineNumber;
        }, true).Cast<VariableExpressionAst>().ToList();

        if (VariableAssignments.Count > 0)
        {
            r = true;
        }
        // Node is probably the first Assignment Statement within scope
        if (node.Parent is AssignmentStatementAst && node.Extent.StartLineNumber >= scope.Extent.StartLineNumber)
        {
            r = true;
        }

        return r;
    }

    private static bool WithinTargetsScope(Ast Target, Ast Child)
    {
        bool r = false;
        Ast childParent = Child.Parent;
        Ast TargetScope = GetAstParentScope(Target);
        while (childParent != null)
        {
            if (childParent is FunctionDefinitionAst FuncDefAst)
            {
                if (Child is VariableExpressionAst VarExpAst && !IsVariableExpressionAssignedInTargetScope(VarExpAst, FuncDefAst))
                {

                }
                else
                {
                    break;
                }
            }
            if (childParent == TargetScope)
            {
                break;
            }
            childParent = childParent.Parent;
        }
        if (childParent == TargetScope)
        {
            r = true;
        }
        return r;
    }

    private class NodeProcessingState
    {
        public Ast Node { get; set; }
        public IEnumerator<Ast> ChildrenEnumerator { get; set; }
    }

    internal void Visit(Ast root)
    {
        Stack<NodeProcessingState> processingStack = new();

        processingStack.Push(new NodeProcessingState { Node = root });

        while (processingStack.Count > 0)
        {
            NodeProcessingState currentState = processingStack.Peek();

            if (currentState.ChildrenEnumerator == null)
            {
                // First time processing this node. Do the initial processing.
                ProcessNode(currentState.Node);  // This line is crucial.

                // Get the children and set up the enumerator.
                IEnumerable<Ast> children = currentState.Node.FindAll(ast => ast.Parent == currentState.Node, searchNestedScriptBlocks: true);
                currentState.ChildrenEnumerator = children.GetEnumerator();
            }

            // Process the next child.
            if (currentState.ChildrenEnumerator.MoveNext())
            {
                Ast child = currentState.ChildrenEnumerator.Current;
                processingStack.Push(new NodeProcessingState { Node = child });
            }
            else
            {
                // All children have been processed, we're done with this node.
                processingStack.Pop();
            }
        }
    }

    private void ProcessNode(Ast node)
    {

        switch (node)
        {
            case CommandAst commandAst:
                ProcessCommandAst(commandAst);
                break;
            case CommandParameterAst commandParameterAst:
                ProcessCommandParameterAst(commandParameterAst);
                break;
            case VariableExpressionAst variableExpressionAst:
                ProcessVariableExpressionAst(variableExpressionAst);
                break;
        }
    }

    private void ProcessCommandAst(CommandAst commandAst)
    {
        // Is the Target Variable a Parameter and is this commandAst the target function
        if (isParam && commandAst.GetCommandName()?.ToLower() == TargetFunction?.Name.ToLower())
        {
            // Check to see if this is a splatted call to the target function.
            Ast Splatted = null;
            foreach (Ast element in commandAst.CommandElements)
            {
                if (element is VariableExpressionAst varAst && varAst.Splatted)
                {
                    Splatted = varAst;
                    break;
                }
            }
            if (Splatted != null)
            {
                NewSplattedModification(Splatted);
            }
            else
            {
                // The Target Variable is a Parameter and the commandAst is the Target Function
                ShouldRename = true;
            }
        }
    }

    private void ProcessVariableExpressionAst(VariableExpressionAst variableExpressionAst)
    {
        if (variableExpressionAst.VariablePath.UserPath.ToLower() == OldName.ToLower())
        {
            // Is this the Target Variable
            if (variableExpressionAst.Extent.StartColumnNumber == StartColumnNumber &&
            variableExpressionAst.Extent.StartLineNumber == StartLineNumber)
            {
                ShouldRename = true;
                TargetVariableAst = variableExpressionAst;
            }
            // Is this a Command Ast within scope
            else if (variableExpressionAst.Parent is CommandAst commandAst)
            {
                if (WithinTargetsScope(TargetVariableAst, commandAst))
                {
                    ShouldRename = true;
                }
                // The TargetVariable is defined within a function
                // This commandAst is not within that function's scope so we should not rename
                if (GetAstParentScope(TargetVariableAst) is FunctionDefinitionAst && !WithinTargetsScope(TargetVariableAst, commandAst))
                {
                    ShouldRename = false;
                }

            }
            // Is this a Variable Assignment thats not within scope
            else if (variableExpressionAst.Parent is AssignmentStatementAst assignment &&
                assignment.Operator == TokenKind.Equals)
            {
                if (!WithinTargetsScope(TargetVariableAst, variableExpressionAst))
                {
                    ShouldRename = false;
                }

            }
            // Else is the variable within scope
            else
            {
                ShouldRename = WithinTargetsScope(TargetVariableAst, variableExpressionAst);
            }
            if (ShouldRename)
            {
                // have some modifications to account for the dollar sign prefix powershell uses for variables
                TextEdit Change = new()
                {
                    NewText = NewName.Contains("$") ? NewName : "$" + NewName,
                    Range = new ScriptExtentAdapter(variableExpressionAst.Extent),
                };
                // If the variables parent is a parameterAst Add a modification
                if (variableExpressionAst.Parent is ParameterAst paramAst && !AliasSet &&
                    CreateAlias)
                {
                    TextEdit aliasChange = NewParameterAliasChange(variableExpressionAst, paramAst);
                    Edits.Add(aliasChange);
                    AliasSet = true;
                }
                Edits.Add(Change);

            }
        }
    }

    private void ProcessCommandParameterAst(CommandParameterAst commandParameterAst)
    {
        if (commandParameterAst.ParameterName.ToLower() == OldName.ToLower())
        {
            if (commandParameterAst.Extent.StartLineNumber == StartLineNumber &&
                commandParameterAst.Extent.StartColumnNumber == StartColumnNumber)
            {
                ShouldRename = true;
            }

            if (TargetFunction != null && commandParameterAst.Parent is CommandAst commandAst &&
                commandAst.GetCommandName().ToLower() == TargetFunction.Name.ToLower() && isParam && ShouldRename)
            {
                TextEdit Change = new()
                {
                    NewText = NewName.Contains("-") ? NewName : "-" + NewName,
                    Range = new ScriptExtentAdapter(commandParameterAst.Extent)
                };
                Edits.Add(Change);
            }
            else
            {
                ShouldRename = false;
            }
        }
    }

    private void NewSplattedModification(Ast Splatted)
    {
        // This Function should be passed a splatted VariableExpressionAst which
        // is used by a CommandAst that is the TargetFunction.

        // Find the splats top assignment / definition
        Ast SplatAssignment = GetVariableTopAssignment(
            Splatted.Extent.StartLineNumber,
            Splatted.Extent.StartColumnNumber,
            ScriptAst);
        // Look for the Parameter within the Splats HashTable
        if (SplatAssignment.Parent is AssignmentStatementAst assignmentStatementAst &&
        assignmentStatementAst.Right is CommandExpressionAst commExpAst &&
        commExpAst.Expression is HashtableAst hashTableAst)
        {
            foreach (Tuple<ExpressionAst, StatementAst> element in hashTableAst.KeyValuePairs)
            {
                if (element.Item1 is StringConstantExpressionAst strConstAst &&
                strConstAst.Value.ToLower() == OldName.ToLower())
                {
                    TextEdit Change = new()
                    {
                        NewText = NewName,
                        Range = new ScriptExtentAdapter(strConstAst.Extent)
                    };

                    Edits.Add(Change);
                    break;
                }

            }
        }
    }

    private TextEdit NewParameterAliasChange(VariableExpressionAst variableExpressionAst, ParameterAst paramAst)
    {
        // Check if an Alias AttributeAst already exists and append the new Alias to the existing list
        // Otherwise Create a new Alias Attribute
        // Add the modifications to the changes
        // The Attribute will be appended before the variable or in the existing location of the original alias
        TextEdit aliasChange = new();
        // FIXME: Understand this more, if this returns more than one result, why does it overwrite the aliasChange?
        foreach (Ast Attr in paramAst.Attributes)
        {
            if (Attr is AttributeAst AttrAst)
            {
                // Alias Already Exists
                if (AttrAst.TypeName.FullName == "Alias")
                {
                    string existingEntries = AttrAst.Extent.Text
                    .Substring("[Alias(".Length);
                    existingEntries = existingEntries.Substring(0, existingEntries.Length - ")]".Length);
                    string nentries = existingEntries + $", \"{OldName}\"";

                    aliasChange = aliasChange with
                    {
                        NewText = $"[Alias({nentries})]",
                        Range = new ScriptExtentAdapter(AttrAst.Extent)
                    };
                }
            }
        }
        if (aliasChange.NewText == null)
        {
            aliasChange = aliasChange with
            {
                NewText = $"[Alias(\"{OldName}\")]",
                Range = new ScriptExtentAdapter(paramAst.Extent)
            };
        }

        return aliasChange;
    }

    internal TextEdit[] VisitAndGetEdits()
    {
        Visit(ScriptAst);
        return Edits.ToArray();
    }
}
#nullable enable

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
