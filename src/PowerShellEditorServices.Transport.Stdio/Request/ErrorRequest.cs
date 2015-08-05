//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Utility;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("geterr")]
    public class ErrorRequest : RequestBase<ErrorRequestArguments>
    {
        private static CancellationTokenSource existingRequestCancellation;

        public static ErrorRequest Create(params string[] filePaths)
        {
            return new ErrorRequest
            {
                Arguments = new ErrorRequestArguments
                {
                    Files = filePaths
                }
            };
        }

        public override Task ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            List<ScriptFile> fileList = new List<ScriptFile>();

            // If there's an existing task, attempt to cancel it
            try
            {
                if (existingRequestCancellation != null)
                {
                    // Try to cancel the request
                    existingRequestCancellation.Cancel();

                    // If cancellation didn't throw an exception,
                    // clean up the existing token
                    existingRequestCancellation.Dispose();
                    existingRequestCancellation = null;
                }
            }
            catch (Exception e)
            {
                // TODO: Catch a more specific exception!
                Logger.Write(
                    LogLevel.Error,
                    string.Format(
                        "Exception while cancelling analysis task:\n\n{0}",
                        e.ToString()));

                return TaskConstants.Canceled;
            }

            // Create a fresh cancellation token and then start the task.
            // We create this on a different TaskScheduler so that we
            // don't block the main message loop thread.
            // TODO: Is there a better way to do this?
            existingRequestCancellation = new CancellationTokenSource();
            Task.Factory.StartNew(
                () =>
                    DelayThenInvokeDiagnostics(
                        this.Arguments.Delay,
                        this.Arguments.Files,
                        editorSession,
                        messageWriter,
                        existingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return TaskConstants.Completed;
        }

        private static async Task DelayThenInvokeDiagnostics(
            int delayMilliseconds,
            string[] filesToAnalyze,
            EditorSession editorSession,
            MessageWriter messageWriter,
            CancellationToken cancellationToken)
        {
            // First of all, wait for the desired delay period before
            // analyzing the provided list of files
            try
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // If the task is cancelled, exit directly
                return;
            }

            // If we've made it past the delay period then we don't care
            // about the cancellation token anymore.  This could happen
            // when the user stops typing for long enough that the delay
            // period ends but then starts typing while analysis is going
            // on.  It makes sense to send back the results from the first
            // delay period while the second one is ticking away.

            // Get the requested files
            foreach (string filePath in filesToAnalyze)
            {
                ScriptFile scriptFile = 
                    editorSession.Workspace.GetFile(
                        filePath);

                var semanticMarkers =
                    editorSession.AnalysisService.GetSemanticMarkers(
                        scriptFile);

                // Always send syntax and semantic errors.  We want to 
                // make sure no out-of-date markers are being displayed.
                messageWriter.WriteMessage(
                    SyntaxDiagnosticEvent.Create(
                        scriptFile.FilePath,
                        scriptFile.SyntaxMarkers));

                messageWriter.WriteMessage(
                    SemanticDiagnosticEvent.Create(
                        scriptFile.FilePath,
                        semanticMarkers));
            }
        }
    }

    public class ErrorRequestArguments
    {
        public string[] Files { get; set; }

        public int Delay { get; set; }
    }
}
