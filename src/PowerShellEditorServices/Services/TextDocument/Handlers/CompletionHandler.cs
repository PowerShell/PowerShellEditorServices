// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal record CompletionResults(bool IsIncomplete, IReadOnlyList<CompletionItem> Matches);

    internal class PsesCompletionHandler : CompletionHandlerBase
    {
        private readonly ILogger _logger;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;
        private CompletionCapability _completionCapability;

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

        protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            _completionCapability = capability;
            return new CompletionRegistrationOptions()
            {
                // TODO: What do we do with the arguments?
                DocumentSelector = LspUtils.PowerShellDocumentSelector,
                ResolveProvider = true,
                TriggerCharacters = new[] { ".", "-", ":", "\\", "$", " " },
            };
        }

        public bool SupportsSnippets => _completionCapability?.CompletionItem?.SnippetSupport is true;

        public bool SupportsCommitCharacters => _completionCapability?.CompletionItem?.CommitCharactersSupport is true;

        public bool SupportsMarkdown => _completionCapability?.CompletionItem?.DocumentationFormat?.Contains(MarkupKind.Markdown) is true;

        public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            int cursorLine = request.Position.Line + 1;
            int cursorColumn = request.Position.Character + 1;

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            try
            {
                (bool isIncomplete, IReadOnlyList<CompletionItem> completionResults) = await GetCompletionsInFileAsync(
                    scriptFile,
                    cursorLine,
                    cursorColumn,
                    cancellationToken).ConfigureAwait(false);

                // Treat completions triggered by space as incomplete so that `gci `
                // and then typing `-` doesn't just filter the list of parameter values
                // (typically files) returned by the space completion
                return new CompletionList(completionResults, isIncomplete || request?.Context?.TriggerCharacter is " ");
            }
            // Ignore canceled requests (logging will pollute the output).
            catch (TaskCanceledException)
            {
                return new CompletionList(isIncomplete: true);
            }
            // We can't do anything about completions failing.
            catch (Exception e)
            {
                _logger.LogWarning(e, "Exception occurred while running handling completion request");
                return new CompletionList(isIncomplete: true);
            }
        }

        // Handler for "completionItem/resolve". In VSCode this is fired when a completion item is highlighted in the completion list.
        public override async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            if (SupportsMarkdown)
            {
                if (request.Kind is CompletionItemKind.Method)
                {
                    string documentation = FormatUtils.GetMethodDocumentation(
                        _logger,
                        request.Data.ToString(),
                        out MarkupKind kind);

                    return request with
                    {
                        Documentation = new MarkupContent()
                        {
                            Kind = kind,
                            Value = documentation,
                        },
                    };
                }

                if (request.Kind is CompletionItemKind.Class or CompletionItemKind.TypeParameter or CompletionItemKind.Enum)
                {
                    string documentation = FormatUtils.GetTypeDocumentation(
                        _logger,
                        request.Detail,
                        out MarkupKind kind);

                    return request with
                    {
                        Detail = null,
                        Documentation = new MarkupContent()
                        {
                            Kind = kind,
                            Value = documentation,
                        },
                    };
                }

                if (request.Kind is CompletionItemKind.EnumMember or CompletionItemKind.Property or CompletionItemKind.Field)
                {
                    string documentation = FormatUtils.GetPropertyDocumentation(
                        _logger,
                        request.Data.ToString(),
                        out MarkupKind kind);

                    return request with
                    {
                        Documentation = new MarkupContent()
                        {
                            Kind = kind,
                            Value = documentation,
                        },
                    };
                }
            }

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
        /// <param name="cancellationToken">The token used to cancel this.</param>
        /// <returns>
        /// A CommandCompletion instance completions for the identified statement.
        /// </returns>
        internal async Task<CompletionResults> GetCompletionsInFileAsync(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber,
            CancellationToken cancellationToken)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            CommandCompletion result = await AstOperations.GetCompletionsAsync(
                scriptFile.ScriptAst,
                scriptFile.ScriptTokens,
                scriptFile.GetOffsetAtPosition(lineNumber, columnNumber),
                _executionService,
                _logger,
                cancellationToken).ConfigureAwait(false);

            if (!(result?.CompletionMatches?.Count > 0))
            {
                return new CompletionResults(IsIncomplete: true, Array.Empty<CompletionItem>());
            }

            BufferRange replacedRange = scriptFile.GetRangeBetweenOffsets(
                result.ReplacementIndex,
                result.ReplacementIndex + result.ReplacementLength);

            string textToBeReplaced = string.Empty;
            if (result.ReplacementLength is not 0)
            {
                textToBeReplaced = scriptFile.Contents.Substring(
                    result.ReplacementIndex,
                    result.ReplacementLength);
            }

            bool isIncomplete = false;
            // Create OmniSharp CompletionItems from PowerShell CompletionResults. We use a for loop
            // because the index is used for sorting.
            CompletionItem[] completionItems = new CompletionItem[result.CompletionMatches.Count];
            for (int i = 0; i < result.CompletionMatches.Count; i++)
            {
                CompletionResult completionMatch = result.CompletionMatches[i];

                // If a completion result is a variable scope like `$script:` we want to
                // mark as incomplete so on typing `:` completion changes.
                if (completionMatch.ResultType is CompletionResultType.Variable
                    && completionMatch.CompletionText.EndsWith(":"))
                {
                    isIncomplete = true;
                }

                completionItems[i] = CreateCompletionItem(
                    result.CompletionMatches[i],
                    replacedRange,
                    i + 1,
                    textToBeReplaced,
                    scriptFile);

                _logger.LogTrace("Created completion item: " + completionItems[i] + " with " + completionItems[i].TextEdit);
            }

            return new CompletionResults(isIncomplete, completionItems);
        }

        internal CompletionItem CreateCompletionItem(
            CompletionResult result,
            BufferRange completionRange,
            int sortIndex,
            string textToBeReplaced,
            ScriptFile scriptFile)
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
                FilterText = result.ResultType is CompletionResultType.Type
                    ? GetTypeFilterText(textToBeReplaced, result.CompletionText)
                    : result.CompletionText,
                // Used instead of Label when TextEdit is unsupported
                InsertText = result.CompletionText,
                // Used instead of InsertText when possible
                TextEdit = textEdit
            };

            return result.ResultType switch
            {
                CompletionResultType.Text => item with { Kind = CompletionItemKind.Text },
                CompletionResultType.History => item with { Kind = CompletionItemKind.Reference },
                CompletionResultType.Command => item with { Kind = CompletionItemKind.Function },
                CompletionResultType.ProviderItem or CompletionResultType.ProviderContainer
                    => CreateProviderItemCompletion(item, result, scriptFile, textToBeReplaced),
                CompletionResultType.Property => item with
                {
                    Kind = CompletionItemKind.Property,
                    Detail = SupportsMarkdown ? null : detail,
                    Data = SupportsMarkdown ? detail : null,
                    CommitCharacters = MaybeAddCommitCharacters("."),
                },
                CompletionResultType.Method => item with
                {
                    Kind = CompletionItemKind.Method,
                    Data = item.Detail,
                    Detail = SupportsMarkdown ? null : item.Detail,
                },
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
                    ? item with { Kind = CompletionItemKind.Class, Detail = detail.Substring("Class ".Length) }
                    : detail.StartsWith("Enum ", StringComparison.CurrentCulture)
                        ? item with { Kind = CompletionItemKind.Enum, Detail = detail.Substring("Enum ".Length) }
                        : item with { Kind = CompletionItemKind.TypeParameter },
                CompletionResultType.Keyword or CompletionResultType.DynamicKeyword =>
                    item with { Kind = CompletionItemKind.Keyword },
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            };
        }

        private CompletionItem CreateProviderItemCompletion(
            CompletionItem item,
            CompletionResult result,
            ScriptFile scriptFile,
            string textToBeReplaced)
        {
            // TODO: Work out a way to do this generally instead of special casing PSScriptRoot.
            //
            // This code relies on PowerShell/PowerShell#17376. Until that makes it into a release
            // no matches will be returned anyway.
            const string PSScriptRootVariable = "$PSScriptRoot";
            string completionText = result.CompletionText;
            if (textToBeReplaced.IndexOf(PSScriptRootVariable, StringComparison.OrdinalIgnoreCase) is int variableIndex and not -1
                && System.IO.Path.GetDirectoryName(scriptFile.FilePath) is string scriptFolder and not ""
                && completionText.IndexOf(scriptFolder, StringComparison.OrdinalIgnoreCase) is int pathIndex and not -1
                && !scriptFile.IsInMemory)
            {
                completionText = completionText
                    .Remove(pathIndex, scriptFolder.Length)
                    .Insert(variableIndex, textToBeReplaced.Substring(variableIndex, PSScriptRootVariable.Length));
            }

            InsertTextFormat insertFormat;
            TextEdit edit;
            CompletionItemKind itemKind;
            if (result.ResultType is CompletionResultType.ProviderContainer
                && SupportsSnippets
                && TryBuildSnippet(completionText, out string snippet))
            {
                edit = item.TextEdit.TextEdit with { NewText = snippet };
                insertFormat = InsertTextFormat.Snippet;
                itemKind = CompletionItemKind.Folder;
            }
            else
            {
                edit = item.TextEdit.TextEdit with { NewText = completionText };
                insertFormat = default;
                itemKind = CompletionItemKind.File;
            }

            return item with
            {
                Kind = itemKind,
                TextEdit = edit,
                InsertText = completionText,
                FilterText = completionText,
                InsertTextFormat = insertFormat,
            };
        }

        private Container<string> MaybeAddCommitCharacters(params string[] characters)
            => SupportsCommitCharacters ? new Container<string>(characters) : null;

        private static string GetTypeFilterText(string textToBeReplaced, string completionText)
        {
            // FilterText for a type name with using statements gets a little complicated. Consider
            // this script:
            //
            // using namespace System.Management.Automation
            // [System.Management.Automation.Tracing.]
            //
            // Since we're emitting an edit that replaces `System.Management.Automation.Tracing.` with
            // `Tracing.NullWriter` (for example), we can't use CompletionText as the filter. If we
            // do, we won't find any matches because it's trying to filter `Tracing.NullWriter` with
            // `System.Management.Automation.Tracing.` which is too different. So we prepend each
            // namespace that exists in our original text but does not in our completion text.
            if (!textToBeReplaced.Contains('.'))
            {
                return completionText;
            }

            string[] oldTypeParts = textToBeReplaced.Split('.');
            string[] newTypeParts = completionText.Split('.');

            StringBuilder newFilterText = new(completionText);

            int newPartsIndex = newTypeParts.Length - 2;
            for (int i = oldTypeParts.Length - 2; i >= 0; i--)
            {
                if (newPartsIndex is >= 0
                    && newTypeParts[newPartsIndex].Equals(oldTypeParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    newPartsIndex--;
                    continue;
                }

                newFilterText.Insert(0, '.').Insert(0, oldTypeParts[i]);
            }

            return newFilterText.ToString();
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
                    .Replace("}", @"\}")
                    .Replace("$", @"\$");
                snippet = sb.Insert(sb.Length - 1, "$0").ToString();
                return true;
            }
            return false;
        }
    }
}
