//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// EnvDTE assembly references currently don't seem to be working
// with the new dotnet cli tools, disabling this temporarily.
#if UseEnvDTE

using EnvDTE;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    internal class AttachHelper
    {
        public static void AttachToProcessIfDebugging(int processId)
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

#endif