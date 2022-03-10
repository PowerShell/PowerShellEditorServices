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
            // We currently only support this request for anything that returns a CommandInfo:
            // functions, cmdlets, aliases. No detail means the module hasn't been imported yet and
            // IntelliSense shouldn't import the module to get this info.
            if (request.Kind is not CompletionItemKind.Function || request.Detail is null)
            {
                return request;
            }

            // Get the documentation for the function
            CommandInfo commandInfo = await CommandHelpers.GetCommandInfoAsync(
                request.Label,
                _runspaceContext.CurrentRunspace,
                _executionService,
                cancellationToken).ConfigureAwait(false);

            if (commandInfo is not null)
            {
                return request with
                {
                    Documentation = await CommandHelpers.GetCommandSynopsisAsync(
                        commandInfo,
                        _executionService,
                        cancellationToken).ConfigureAwait(false)
                };
            }

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
            CompletionResult result,
            BufferRange completionRange,
            int sortIndex)
        {
            Validate.IsNotNull(nameof(result), result);

            TextEdit textEdit = new()
            {
                NewText = result.CompletionText,
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
            };

            // Some tooltips may have newlines or whitespace for unknown reasons.
            string detail = result.ToolTip?.Trim();

            CompletionItem item = new()
            {
                Label = result.ListItemText,
                Detail = result.ListItemText.Equals(detail, StringComparison.CurrentCulture)
                    ? string.Empty : detail, // Don't repeat label.
                // Retain PowerShell's sort order with the given index.
                SortText = $"{sortIndex:D4}{result.ListItemText}",
                FilterText = result.CompletionText,
                TextEdit = textEdit // Used instead of InsertText.
            };

            return result.ResultType switch
            {
                CompletionResultType.Text => item with { Kind = CompletionItemKind.Text },
                CompletionResultType.History => item with { Kind = CompletionItemKind.Reference },
                CompletionResultType.Command => item with { Kind = CompletionItemKind.Function },
                CompletionResultType.ProviderItem => item with { Kind = CompletionItemKind.File },
                CompletionResultType.ProviderContainer => TryBuildSnippet(result.CompletionText, out string snippet)
                    ? item with
                    {
                        Kind = CompletionItemKind.Folder,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        TextEdit = textEdit with { NewText = snippet }
                    }
                    : item with { Kind = CompletionItemKind.Folder },
                CompletionResultType.Property => item with { Kind = CompletionItemKind.Property },
                CompletionResultType.Method => item with { Kind = CompletionItemKind.Method },
                CompletionResultType.ParameterName => TryExtractType(detail, out string type)
                    ? item with { Kind = CompletionItemKind.Variable, Detail = type }
                    // The comparison operators (-eq, -not, -gt, etc) unfortunately come across as
                    // ParameterName types but they don't have a type associated to them, so we can
                    // deduce it is an operator.
                    : item with { Kind = CompletionItemKind.Operator },
                CompletionResultType.ParameterValue => item with { Kind = CompletionItemKind.Value },
                CompletionResultType.Variable => TryExtractType(detail, out string type)
                    ? item with { Kind = CompletionItemKind.Variable, Detail = type }
                    : item with { Kind = CompletionItemKind.Variable },
                CompletionResultType.Namespace => item with { Kind = CompletionItemKind.Module },
                CompletionResultType.Type => detail.StartsWith("Class ", StringComparison.CurrentCulture)
                    // Custom classes come through as types but the PowerShell completion tooltip
                    // will start with "Class ", so we can more accurately display its icon.
                    ? item with { Kind = CompletionItemKind.Class }
                    : item with { Kind = CompletionItemKind.TypeParameter },
                CompletionResultType.Keyword or CompletionResultType.DynamicKeyword =>
                    item with { Kind = CompletionItemKind.Keyword },
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            };
        }

        private static readonly Regex s_typeRegex = new(@"^(\[.+\])", RegexOptions.Compiled);

        /// <summary>
        /// Look for type encoded in the tooltip for parameters and variables. Display PowerShell
        /// type names in [] to be consistent with PowerShell syntax and how the debugger displays
        /// type names.
        /// </summary>
        /// <param name="toolTipText"></param>
        /// <param name="type"></param>
        /// <returns>Whether or not the type was found.</returns>
        private static bool TryExtractType(string toolTipText, out string type)
        {
            MatchCollection matches = s_typeRegex.Matches(toolTipText);
            type = string.Empty;
            if ((matches.Count > 0) && (matches[0].Groups.Count > 1))
            {
                type = matches[0].Groups[1].Value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Insert a final "tab stop" as identified by $0 in the snippet provided for completion.
        /// For folder paths, we take the path returned by PowerShell e.g. 'C:\Program Files' and
        /// insert the tab stop marker before the closing quote char e.g. 'C:\Program Files$0'. This
        /// causes the editing cursor to be placed *before* the final quote after completion, which
        /// makes subsequent path completions work. See this part of the LSP spec for details:
        /// https://microsoft.github.io/language-server-protocol/specification#textDocument_completion
        /// </summary>
        /// <param name="completionText"></param>
        /// <param name="snippet"></param>
        /// <returns>
        /// Whether or not the completion ended with a quote and so was a snippet.
        /// </returns>
        private static bool TryBuildSnippet(string completionText, out string snippet)
        {
            snippet = string.Empty;
            if (!string.IsNullOrEmpty(completionText)
                && completionText[completionText.Length - 1] is '"' or '\'')
            {
                // Since we want to use a "tab stop" we need to escape a few things.
                StringBuilder sb = new StringBuilder(completionText)
                    .Replace(@"\", @"\\")
                    .Replace(@"}", @"\}")
                    .Replace(@"$", @"\$");
                snippet = sb.Insert(sb.Length - 1, "$0").ToString();
                return true;
            }
            return false;
        }
    }
}
