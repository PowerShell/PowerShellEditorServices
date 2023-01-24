// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides common operations for the syntax tree of a parsed script.
    /// </summary>
    internal static class AstOperations
    {
        private static readonly Func<IScriptPosition, int, IScriptPosition> s_clonePositionWithNewOffset;

        static AstOperations()
        {
            Type internalScriptPositionType = typeof(PSObject).GetTypeInfo().Assembly
                .GetType("System.Management.Automation.Language.InternalScriptPosition");

            MethodInfo cloneWithNewOffsetMethod = internalScriptPositionType.GetMethod("CloneWithNewOffset", BindingFlags.Instance | BindingFlags.NonPublic);

            ParameterExpression originalPosition = Expression.Parameter(typeof(IScriptPosition));
            ParameterExpression newOffset = Expression.Parameter(typeof(int));

            ParameterExpression[] parameters = new ParameterExpression[] { originalPosition, newOffset };
            s_clonePositionWithNewOffset = Expression.Lambda<Func<IScriptPosition, int, IScriptPosition>>(
                Expression.Call(
                    Expression.Convert(originalPosition, internalScriptPositionType),
                    cloneWithNewOffsetMethod,
                    newOffset),
                parameters).Compile();
        }

        /// <summary>
        /// Gets completions for the symbol found in the Ast at
        /// the given file offset.
        /// </summary>
        /// <param name="scriptAst">
        /// The Ast which will be traversed to find a completable symbol.
        /// </param>
        /// <param name="currentTokens">
        /// The array of tokens corresponding to the scriptAst parameter.
        /// </param>
        /// <param name="fileOffset">
        /// The 1-based file offset at which a symbol will be located.
        /// </param>
        /// <param name="executionService">
        /// The PowerShellContext to use for gathering completions.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <param name="cancellationToken">
        /// A CancellationToken to cancel completion requests.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance that contains completions for the
        /// symbol at the given offset.
        /// </returns>
        public static async Task<CommandCompletion> GetCompletionsAsync(
            Ast scriptAst,
            Token[] currentTokens,
            int fileOffset,
            IInternalPowerShellExecutionService executionService,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            IScriptPosition cursorPosition = s_clonePositionWithNewOffset(scriptAst.Extent.StartScriptPosition, fileOffset);

            logger.LogTrace($"Getting completions at offset {fileOffset} (line: {cursorPosition.LineNumber}, column: {cursorPosition.ColumnNumber})");

            Stopwatch stopwatch = new();

            CommandCompletion commandCompletion = null;
            await executionService.ExecuteDelegateAsync(
                representation: "CompleteInput",
                new ExecutionOptions { Priority = ExecutionPriority.Next },
                (pwsh, _) =>
                {
                    stopwatch.Start();

                    // If the current runspace is not out of process, then we call TabExpansion2 so
                    // that we have the ability to issue pipeline stop requests on cancellation.
                    if (executionService is PsesInternalHost psesInternalHost
                        && !psesInternalHost.Runspace.RunspaceIsRemote)
                    {
                        IReadOnlyList<CommandCompletion> completionResults = new SynchronousPowerShellTask<CommandCompletion>(
                            logger,
                            psesInternalHost,
                            new PSCommand()
                                .AddCommand("TabExpansion2")
                                    .AddParameter("ast", scriptAst)
                                    .AddParameter("tokens", currentTokens)
                                    .AddParameter("positionOfCursor", cursorPosition),
                            executionOptions: null,
                            cancellationToken)
                            .ExecuteAndGetResult(cancellationToken);

                        if (completionResults is { Count: > 0 })
                        {
                            commandCompletion = completionResults[0];
                        }

                        return;
                    }

                    // If the current runspace is out of process, we can't call TabExpansion2
                    // because the output will be serialized.
                    commandCompletion = CommandCompletion.CompleteInput(
                        scriptAst,
                        currentTokens,
                        cursorPosition,
                        options: null,
                        powershell: pwsh);
                },
                cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            logger.LogTrace(
                "IntelliSense completed in {elapsed}ms - WordToComplete: \"{word}\" MatchCount: {count}",
                stopwatch.ElapsedMilliseconds,
                commandCompletion.ReplacementLength > 0
                    ? scriptAst.Extent.StartScriptPosition.GetFullScript()?.Substring(
                        commandCompletion.ReplacementIndex,
                        commandCompletion.ReplacementLength)
                    : null,
                commandCompletion.CompletionMatches.Count);

            return commandCompletion;
        }

        internal static bool TryGetInferredValue(ExpandableStringExpressionAst expandableStringExpressionAst, out string value)
        {
            // Currently we only support inferring the value of `$PSScriptRoot`. We could potentially
            // expand this to parts of `$MyInvocation` and some basic constant folding.
            if (string.IsNullOrEmpty(expandableStringExpressionAst.Extent.File))
            {
                value = null;
                return false;
            }

            string psScriptRoot = System.IO.Path.GetDirectoryName(expandableStringExpressionAst.Extent.File);
            if (string.IsNullOrEmpty(psScriptRoot))
            {
                value = null;
                return false;
            }

            string path = expandableStringExpressionAst.Value;
            foreach (ExpressionAst nestedExpression in expandableStringExpressionAst.NestedExpressions)
            {
                // If the string contains the variable $PSScriptRoot, we replace it with the corresponding value.
                if (!(nestedExpression is VariableExpressionAst variableAst
                    && variableAst.VariablePath.UserPath.Equals("PSScriptRoot", StringComparison.OrdinalIgnoreCase)))
                {
                    value = null;
                    return false;
                }

                // TODO: This should use offsets from the extent rather than a blind replace. In
                // practice it won't hurt anything because $ is not valid in paths, but if we expand
                // this functionality, this will be problematic.
                path = path.Replace(variableAst.ToString(), psScriptRoot);
            }

            value = path;
            return true;
        }
    }
}
