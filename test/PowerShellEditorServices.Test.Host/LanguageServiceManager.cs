//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using EnvDTE;
using Microsoft.PowerShell.EditorServices.Transport.Stdio;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class LanguageServiceManager
    {
        Stream inputStream;
        Stream outputStream;
        System.Diagnostics.Process languageServiceProcess;

        public MessageReader MessageReader { get; private set; }

        public MessageWriter MessageWriter { get; private set; }

        public void Start()
        {
            // If the test is running in the debugger, tell the language
            // service to also wait for the debugger
            string languageServiceArguments = string.Empty;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                languageServiceArguments = "/waitForDebugger";
            }

            this.languageServiceProcess = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "Microsoft.PowerShell.EditorServices.Host.exe",
                    Arguments = languageServiceArguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };

            // Start the process
            this.languageServiceProcess.Start();

            // Attach to the language service process if debugging
            if (System.Diagnostics.Debugger.IsAttached)
            {
                AttachToProcessIfDebugging(this.languageServiceProcess.Id);
            }

            // Load up all of the message types from the transport assembly
            MessageTypeResolver messageTypeResolver = new MessageTypeResolver();
            messageTypeResolver.ScanForMessageTypes(typeof(StartedEvent).Assembly);

            // Open the standard input/output streams
            this.inputStream = this.languageServiceProcess.StandardOutput.BaseStream;
            this.outputStream = this.languageServiceProcess.StandardInput.BaseStream;

            // Set up the message reader and writer
            this.MessageReader = 
                new MessageReader(
                    this.inputStream,
                    messageTypeResolver);

            this.MessageWriter = 
                new MessageWriter(
                    this.outputStream,
                    messageTypeResolver);

            // Wait for the 'started' event
            MessageBase startedMessage = this.MessageReader.ReadMessage().Result;
            Assert.IsType<StartedEvent>(startedMessage);
        }

        public void Stop()
        {
            if (this.inputStream != null)
            {
                this.inputStream.Dispose();
                this.inputStream = null;
            }

            if (this.outputStream != null)
            {
                this.outputStream.Dispose();
                this.outputStream = null;
            }

            if (this.MessageReader != null)
            {
                this.MessageReader = null;
            }

            if (this.MessageWriter != null)
            {
                this.MessageWriter = null;
            }

            if (this.languageServiceProcess != null)
            {
                this.languageServiceProcess.Kill();
                this.languageServiceProcess = null;
            }
        }

        private static void AttachToProcessIfDebugging(int processId)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                int tryCount = 5;

                while (tryCount-- > 0)
                {
                    try
                    {
                        var dte = (DTE)Marshal.GetActiveObject("VisualStudio.DTE.12.0");
                        var processes = dte.Debugger.LocalProcesses.OfType<EnvDTE.Process>();
                        var foundProcess = processes.SingleOrDefault(x => x.ProcessID == processId);

                        //EnvDTE.Process foundProcess = null;
                        //for (int i = 0; i < dte.Debugger.LocalProcesses.Count; i++)
                        //{
                        //    foundProcess = dte.Debugger.LocalProcesses.Item(i) as EnvDTE.Process;

                        //    if (foundProcess != null && foundProcess.ProcessID == processId)
                        //    {
                        //        break;
                        //    }
                        //}

                        if (foundProcess != null)
                        {
                            foundProcess.Attach();
                            break;
                        }
                        else
                        {
                            throw new InvalidOperationException("Could not find language service process!");
                        }
                    }
                    catch (COMException)
                    {
                        // Wait a bit and try again
                        System.Threading.Thread.Sleep(1000);
                    }
                }
            }
        }
    }

    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    public class MessageFilter : IOleMessageFilter
    {
        private const int Handled = 0, RetryAllowed = 2, Retry = 99, Cancel = -1, WaitAndDispatch = 2;

        int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            return Handled;
        }

        int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            return dwRejectType == RetryAllowed ? Retry : Cancel;
        }

        int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        {
            return WaitAndDispatch;
        }

        public static void Register()
        {
            CoRegisterMessageFilter(new MessageFilter());
        }

        public static void Revoke()
        {
            CoRegisterMessageFilter(null);
        }

        private static void CoRegisterMessageFilter(IOleMessageFilter newFilter)
        {
            IOleMessageFilter oldFilter;
            CoRegisterMessageFilter(newFilter, out oldFilter);
        }

        [DllImport("Ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
    }
}
