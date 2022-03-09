// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    // TODO: Use ABCs.
    internal class PsesCompletionHandler : ICompletionHandler, ICompletionResolveHandler
    {
        private const int DefaultWaitTimeoutMilliseconds = 5000;
        private readonly ILogger _logger;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;
        private CompletionCapability _capability;
        private readonly Guid _id = Guid.NewGuid();
        private static readonly Regex _typeRegex = new(@"^(\[.+\])");

        Guid ICanBeIdentifiedHandler.Id => _id;

        public PsesCompletionHandler(
            ILoggerFactory factory,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesCompletionHandler>();
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _workspaceService = workspaceService;
        }

        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector,
            ResolveProvider = true,
            TriggerCharacters = new[] { ".", "-", ":", "\\", "$" }
        };

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            int cursorLine = request.Position.Line + 1;
            int cursorColumn = request.Position.Character + 1;

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Completion request canceled for file: {0}", request.TextDocument.Uri);
                return Array.Empty<CompletionItem>();
            }

            IEnumerable<CompletionItem> completionResults = await GetCompletionsInFileAsync(scriptFile, cursorLine, cursorColumn).ConfigureAwait(false);

            return new CompletionList(completionResults);
        }

        public static bool CanResolve(CompletionItem value)
        {
            return value.Kind == CompletionItemKind.Function;
        }

        // Handler for "completionItem/resolve". In VSCode this is fired when a completion item is highlighted in the completion list.
        public async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            // We currently only support this request for anything that returns a CommandInfo: functions, cmdlets, aliases.
            if (request.Kind != CompletionItemKind.Function)
            {
                return request;
            }

            // No details means the module hasn't been imported yet and Intellisense shouldn't import the module to get this info.
            if (request.Detail is null)
            {
                return request;
            }

            // Get the documentation for the function
            CommandInfo commandInfo = await CommandHelpers.GetCommandInfoAsync(request.Label, _runspaceContext.CurrentRunspace, _executionService).ConfigureAwait(false);

            if (commandInfo is not null)
            {
                request = request with
                {
                    Documentation = await CommandHelpers.GetCommandSynopsisAsync(commandInfo, _executionService).ConfigureAwait(false)
                };
            }

            // Send back the updated CompletionItem
            return request;
        }

        public void SetCapability(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            _capability = capability;
        }

        /// <summary>
        /// Gets completions for a statement contained in the given
        /// script file at the specified line and column position.
        /// </summary>
        /// <param name="scriptFile">
        /// The script file in which completions will be gathered.
        /// </param>
        /// <param name="lineNumber">
        /// The 1-based line number at which completions will be gathered.
        /// </param>
        /// <param name="columnNumber">
        /// The 1-based column number at which completions will be gathered.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance completions for the identified statement.
        /// </returns>
        public async Task<IEnumerable<CompletionItem>> GetCompletionsInFileAsync(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            CommandCompletion result = null;
            using (CancellationTokenSource cts = new(DefaultWaitTimeoutMilliseconds))
            {
                result = await AstOperations.GetCompletionsAsync(
                    scriptFile.ScriptAst,
                    scriptFile.ScriptTokens,
                    scriptFile.GetOffsetAtPosition(lineNumber, columnNumber),
                    _executionService,
                    _logger,
                    cts.Token).ConfigureAwait(false);
            }

            // Only calculate the replacement range if there are completions.
            BufferRange replacedRange = new(0, 0, 0, 0);
            if (result.CompletionMatches.Count > 0)
            {
                replacedRange = scriptFile.GetRangeBetweenOffsets(
                    result.ReplacementIndex,
                    result.ReplacementIndex + result.ReplacementLength);
            }

            // Create OmniSharp CompletionItems from PowerShell CompletionResults. We use a for loop
            // because the index is used for sorting.
            CompletionItem[] completionItems = new CompletionItem[result.CompletionMatches.Count];
            for (int i = 0; i < result.CompletionMatches.Count; i++)
            {
                completionItems[i] = CreateCompletionItem(result.CompletionMatches[i], replacedRange, i + 1);
            }
            return completionItems;
        }

        internal static CompletionItem CreateCompletionItem(
            CompletionResult completion,
            BufferRange completionRange,
            int sortIndex)
        {
            Validate.IsNotNull(nameof(completion), completion);

            // Some tooltips may have newlines or whitespace for unknown reasons.
            string toolTipText = completion.ToolTip?.Trim();

            string completionText = completion.CompletionText;
            InsertTextFormat insertTextFormat = InsertTextFormat.PlainText;
            CompletionItemKind kind;

            // Force the client to maintain the sort order in which the original completion results
            // were returned. We just need to make sure the default order also be the
            // lexicographical order which we do by prefixing the ListItemText with a leading 0's
            // four digit index.
            string sortText = $"{sortIndex:D4}{completion.ListItemText}";

            switch (completion.ResultType)
            {
                case CompletionResultType.Command:
                    kind = CompletionItemKind.Function;
                    break;
                case CompletionResultType.History:
                    kind = CompletionItemKind.Reference;
                    break;
                case CompletionResultType.Keyword:
                case CompletionResultType.DynamicKeyword:
                    kind = CompletionItemKind.Keyword;
                    break;
                case CompletionResultType.Method:
                    kind = CompletionItemKind.Method;
                    break;
                case CompletionResultType.Namespace:
                    kind = CompletionItemKind.Module;
                    break;
                case CompletionResultType.ParameterName:
                    kind = CompletionItemKind.Variable;
                    // Look for type encoded in the tooltip for parameters and variables.
                    // Display PowerShell type names in [] to be consistent with PowerShell syntax
                    // and how the debugger displays type names.
                    MatchCollection matches = _typeRegex.Matches(toolTipText);
                    if ((matches.Count > 0) && (matches[0].Groups.Count > 1))
                    {
                        toolTipText = matches[0].Groups[1].Value;
                    }
                    // The comparison operators (-eq, -not, -gt, etc) unfortunately come across as
                    // ParameterName types but they don't have a type associated to them, so we can
                    // deduce its an operator.
                    else
                    {
                        kind = CompletionItemKind.Operator;
                    }
                    break;
                case CompletionResultType.ParameterValue:
                    kind = CompletionItemKind.Value;
                    break;
                case CompletionResultType.Property:
                    kind = CompletionItemKind.Property;
                    break;
                case CompletionResultType.ProviderContainer:
                    kind = CompletionItemKind.Folder;
                    // Insert a final "tab stop" as identified by $0 in the snippet provided for
                    // completion. For folder paths, we take the path returned by PowerShell e.g.
                    // 'C:\Program Files' and insert the tab stop marker before the closing quote
                    // char e.g. 'C:\Program Files$0'. This causes the editing cursor to be placed
                    // *before* the final quote after completion, which makes subsequent path
                    // completions work. See this part of the LSP spec for details:
                    // https://microsoft.github.io/language-server-protocol/specification#textDocument_completion

                    // Since we want to use a "tab stop" we need to escape a few things for Textmate
                    // to render properly.
                    if (EndsWithQuote(completionText))
                    {
                        StringBuilder sb = new StringBuilder(completionText)
                            .Replace(@"\", @"\\")
                            .Replace(@"}", @"\}")
                            .Replace(@"$", @"\$");
                        completionText = sb.Insert(sb.Length - 1, "$0").ToString();
                        insertTextFormat = InsertTextFormat.Snippet;
                    }
                    break;
                case CompletionResultType.ProviderItem:
                    kind = CompletionItemKind.File;
                    break;
                case CompletionResultType.Text:
                    kind = CompletionItemKind.Text;
                    break;
                case CompletionResultType.Type:
                    kind = CompletionItemKind.TypeParameter;
                    // Custom classes come through as types but the PowerShell completion tooltip
                    // will start with "Class ", so we can more accurately display its icon.
                    if (toolTipText.StartsWith("Class ", StringComparison.Ordinal))
                    {
                        kind = CompletionItemKind.Class;
                    }
                    break;
                case CompletionResultType.Variable:
                    kind = CompletionItemKind.Variable;
                    // Look for type encoded in the tooltip for parameters and variables.
                    // Display PowerShell type names in [] to be consistent with PowerShell syntax
                    // and how the debugger displays type names.
                    matches = _typeRegex.Matches(toolTipText);
                    if ((matches.Count > 0) && (matches[0].Groups.Count > 1))
                    {
                        toolTipText = matches[0].Groups[1].Value;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(completion));
            }

            // Don't display tooltip if it is the same as the ListItemText.
            if (completion.ListItemText.Equals(toolTipText, StringComparison.OrdinalIgnoreCase))
            {
                toolTipText = string.Empty;
            }

            Validate.IsNotNull(nameof(CompletionItemKind), kind);

            // TODO: We used to extract the symbol type from the tooltip using a regex, but it
            // wasn't actually used.
            return new CompletionItem
            {
                Kind = kind,
                TextEdit = new TextEdit
                {
                    NewText = completionText,
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = completionRange.Start.Line - 1,
                            Character = completionRange.Start.Column - 1
                        },
                        End = new Position
                        {
                            Line = completionRange.End.Line - 1,
                            Character = completionRange.End.Column - 1
                        }
                    }
                },
                InsertTextFormat = insertTextFormat,
                InsertText = completionText,
                FilterText = completion.CompletionText,
                SortText = sortText,
                // TODO: Documentation
                Detail = toolTipText,
                Label = completion.ListItemText,
                // TODO: Command
            };
        }

        private static bool EndsWithQuote(string text)
        {
            return !string.IsNullOrEmpty(text) && text[text.Length - 1] is '"' or '\'';
        }
    }
}
