//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class ServerTestsBase
    {
        private static int sessionCounter;
        private Process serviceProcess;
        protected IMessageSender messageSender;
        protected IMessageHandlers messageHandlers;

        private ConcurrentDictionary<string, AsyncQueue<object>> eventQueuePerType =
            new ConcurrentDictionary<string, AsyncQueue<object>>();

        private ConcurrentDictionary<string, AsyncQueue<object>> requestQueuePerType =
            new ConcurrentDictionary<string, AsyncQueue<object>>();

        protected async Task<Tuple<string, string>> LaunchService(
            string logPath,
            bool waitForDebugger = false)
        {
            string modulePath = Path.GetFullPath(TestUtilities.NormalizePath("../../../../../module"));
            string scriptPath = Path.GetFullPath(Path.Combine(modulePath, "PowerShellEditorServices", "Start-EditorServices.ps1"));

            if (!File.Exists(scriptPath))
            {
                throw new IOException(String.Format("Bad start script path: '{0}'", scriptPath));
            }

            Assembly assembly = this.GetType().GetTypeInfo().Assembly;

            string assemblyPath = new Uri(assembly.CodeBase).LocalPath;
            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(assemblyPath);

            string sessionPath =
                Path.Combine(
                    Path.GetDirectoryName(assemblyPath), $"session-{++sessionCounter}.json");

            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
            }

            string editorServicesModuleVersion =
                string.Format(
                    "{0}.{1}.{2}",
                    fileVersionInfo.FileMajorPart,
                    fileVersionInfo.FileMinorPart,
                    fileVersionInfo.FileBuildPart);

            string scriptArgs =
                    "\"" + scriptPath + "\" " +
                    "-HostName \\\"PowerShell Editor Services Test Host\\\" " +
                    "-HostProfileId \"Test.PowerShellEditorServices\" " +
                    "-HostVersion \"1.0.0\" " +
                    "-BundledModulesPath \\\"" + modulePath + "\\\" " +
                    "-LogLevel \"Verbose\" " +
                    "-LogPath \"" + logPath + "\" " +
                    "-SessionDetailsPath \"" + sessionPath + "\" " +
                    "-FeatureFlags @() " +
                    "-AdditionalModules @() ";

            if (waitForDebugger)
            {
                scriptArgs += "-WaitForDebugger ";
            }

            string[] args =
                new string[]
                {
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy", "Unrestricted",
                    "-Command \"" + scriptArgs + "\""
                };

            this.serviceProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetPwshExeName(),
                    Arguments = string.Join(" ", args),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true,
            };

            // Start the process
            this.serviceProcess.Start();

            string sessionDetailsText = string.Empty;

            // Wait up to ~5 seconds for the server to finish initializing
            var maxRetryAttempts = 10;
            while (maxRetryAttempts-- > 0)
            {
                if (this.serviceProcess.HasExited)
                {
                    throw new Exception(String.Format("Server host process quit unexpectedly: '{0}'", this.serviceProcess.StandardError.ReadToEnd()));
                }

                try
                {
                    using (var stream = new FileStream(sessionPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (var reader = new StreamReader(stream))
                    {
                        sessionDetailsText = reader.ReadToEnd();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Session details at '{sessionPath}' not available: {ex.Message}");
                }

                Thread.Sleep(500);
            }

            JObject result = JObject.Parse(sessionDetailsText);
            if (result["status"].Value<string>() == "started")
            {
                return new Tuple<string, string>(
                    result["languageServicePipeName"].Value<string>(),
                    result["debugServicePipeName"].Value<string>());
            }

            Debug.WriteLine($"Failed to read session details from '{sessionPath}'");

            return null;
        }

        protected void KillService()
        {
            try
            {
                this.serviceProcess.Kill();
            }
            catch (InvalidOperationException)
            {
                // This exception gets thrown if the server process has
                // already existed by the time Kill gets called.
            }
        }

        protected Task<TResult> SendRequest<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            TParams requestParams)
        {
            return
                this.messageSender.SendRequestAsync(
                    requestType,
                    requestParams,
                    true);
        }

        protected Task SendEvent<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType, TParams eventParams)
        {
            return
                this.messageSender.SendEventAsync(
                    eventType,
                    eventParams);
        }

        protected void QueueEventsForType<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType)
        {
            var eventQueue =
                this.eventQueuePerType.AddOrUpdate(
                    eventType.Method,
                    new AsyncQueue<object>(),
                    (key, queue) => queue);

            this.messageHandlers.SetEventHandler(
                eventType,
                (p, ctx) =>
                {
                    return eventQueue.EnqueueAsync(p);
                });
        }

        protected async Task<TParams> WaitForEvent<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            int timeoutMilliseconds = 5000)
        {
            Task<TParams> eventTask = null;

            // Use the event queue if one has been registered
            AsyncQueue<object> eventQueue = null;
            if (this.eventQueuePerType.TryGetValue(eventType.Method, out eventQueue))
            {
                eventTask =
                    eventQueue
                        .DequeueAsync()
                        .ContinueWith<TParams>(
                            task => (TParams)task.Result);
            }
            else
            {
                TaskCompletionSource<TParams> eventTaskSource = new TaskCompletionSource<TParams>();

                this.messageHandlers.SetEventHandler(
                    eventType,
                    (p, ctx) =>
                    {
                        if (!eventTaskSource.Task.IsCompleted)
                        {
                            eventTaskSource.SetResult(p);
                        }

                        return Task.FromResult(true);
                    });

                eventTask = eventTaskSource.Task;
            }

            await
                Task.WhenAny(
                    eventTask,
                    Task.Delay(timeoutMilliseconds));

            if (!eventTask.IsCompleted)
            {
                throw new TimeoutException(
                    string.Format(
                        "Timed out waiting for '{0}' event!",
                        eventType.Method));
            }

            return await eventTask;
        }

        protected async Task<Tuple<TParams, RequestContext<TResponse>>> WaitForRequest<TParams, TResponse>(
            RequestType<TParams, TResponse, object, object> requestType,
            int timeoutMilliseconds = 5000)
        {
            Task<Tuple<TParams, RequestContext<TResponse>>> requestTask = null;

            // Use the request queue if one has been registered
            AsyncQueue<object> requestQueue = null;
            if (this.requestQueuePerType.TryGetValue(requestType.Method, out requestQueue))
            {
                requestTask =
                    requestQueue
                        .DequeueAsync()
                        .ContinueWith(
                            task => (Tuple<TParams, RequestContext<TResponse>>)task.Result);
            }
            else
            {
                var requestTaskSource =
                    new TaskCompletionSource<Tuple<TParams, RequestContext<TResponse>>>();

                this.messageHandlers.SetRequestHandler(
                    requestType,
                    (p, ctx) =>
                    {
                        if (!requestTaskSource.Task.IsCompleted)
                        {
                            requestTaskSource.SetResult(
                                new Tuple<TParams, RequestContext<TResponse>>(p, ctx));
                        }

                        return Task.FromResult(true);
                    });

                requestTask = requestTaskSource.Task;
            }

            await
                Task.WhenAny(
                    requestTask,
                    Task.Delay(timeoutMilliseconds));

            if (!requestTask.IsCompleted)
            {
                throw new TimeoutException(
                    string.Format(
                        "Timed out waiting for '{0}' request!",
                        requestType.Method));
            }

            return await requestTask;
        }

        private static string GetPwshExeName()
        {
#if !CoreCLR
            return "powershell.exe";
#else
            return "pwsh";
#endif
        }
    }
}
