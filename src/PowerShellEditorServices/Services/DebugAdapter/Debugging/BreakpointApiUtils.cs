//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter

{
    internal static class BreakpointApiUtils
    {
        #region Private Static Fields

        private static readonly Lazy<Func<Debugger, string, int, int, ScriptBlock, LineBreakpoint>> s_setLineBreakpointLazy;

        private static readonly Lazy<Func<Debugger, string, ScriptBlock, string, CommandBreakpoint>> s_setCommandBreakpointLazy;

        private static readonly Lazy<Func<Debugger, List<Breakpoint>>> s_getBreakpointsLazy;

        private static readonly Lazy<Func<Debugger, Breakpoint, bool>> s_removeBreakpointLazy;

        private static readonly Lazy<Func<string, int, int, ScriptBlock, LineBreakpoint>> s_newLineBreakpointLazy;

        private static readonly Lazy<Func<string, WildcardPattern, string, ScriptBlock, CommandBreakpoint>> s_newCommandBreakpointLazy;

        #endregion

        #region Static Constructor

        static BreakpointApiUtils()
        {
            // If this version of PowerShell does not support the new Breakpoint APIs introduced in PowerShell 7.0.0-preview.4,
            // do nothing as this class will not get used.
            if (typeof(Debugger).GetMethod("SetLineBreakpoint", BindingFlags.Public | BindingFlags.Instance) == null)
            {
                return;
            }

            s_setLineBreakpointLazy = new Lazy<Func<Debugger, string, int, int, ScriptBlock, LineBreakpoint>>(() =>
            {
                MethodInfo setLineBreakpointMethod = typeof(Debugger).GetMethod("SetLineBreakpoint", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, string, int, int, ScriptBlock, LineBreakpoint>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, string, int, int, ScriptBlock, LineBreakpoint>),
                    firstArgument: null,
                    setLineBreakpointMethod);
            });

            s_setCommandBreakpointLazy = new Lazy<Func<Debugger, string, ScriptBlock, string, CommandBreakpoint>>(() =>
            {
                MethodInfo setCommandBreakpointMethod = typeof(Debugger).GetMethod("SetCommandBreakpoint", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, string, ScriptBlock, string, CommandBreakpoint>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, string, ScriptBlock, string, CommandBreakpoint>),
                    firstArgument: null,
                    setCommandBreakpointMethod);
            });

            s_getBreakpointsLazy = new Lazy<Func<Debugger, List<Breakpoint>>>(() =>
            {
                MethodInfo removeBreakpointMethod = typeof(Debugger).GetMethod("GetBreakpoints", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, List<Breakpoint>>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, List<Breakpoint>>),
                    firstArgument: null,
                    removeBreakpointMethod);
            });

            s_removeBreakpointLazy = new Lazy<Func<Debugger, Breakpoint, bool>>(() =>
            {
                MethodInfo removeBreakpointMethod = typeof(Debugger).GetMethod("RemoveBreakpoint", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, Breakpoint, bool>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, Breakpoint, bool>),
                    firstArgument: null,
                    removeBreakpointMethod);
            });
        }

        #endregion

        #region Public Static Properties

        private static Func<Debugger, string, int, int, ScriptBlock, LineBreakpoint> SetLineBreakpointDelegate => s_setLineBreakpointLazy.Value;

        private static Func<Debugger, string, ScriptBlock, string, CommandBreakpoint> SetCommandBreakpointDelegate => s_setCommandBreakpointLazy.Value;

        private static Func<Debugger, List<Breakpoint>> GetBreakpointsDelegate => s_getBreakpointsLazy.Value;

        private static Func<Debugger, Breakpoint, bool> RemoveBreakpointDelegate => s_removeBreakpointLazy.Value;

        private static Func<string, int, int, ScriptBlock, LineBreakpoint> CreateLineBreakpointDelegate => s_newLineBreakpointLazy.Value;

        private static Func<string, WildcardPattern, string, ScriptBlock, CommandBreakpoint> CreateCommandBreakpointDelegate => s_newCommandBreakpointLazy.Value;

        #endregion

        #region Public Static Methods

        public static IEnumerable<Breakpoint> SetBreakpoints(Debugger debugger, IEnumerable<BreakpointDetailsBase> breakpoints)
        {
            var psBreakpoints = new List<Breakpoint>(breakpoints.Count());

            foreach (BreakpointDetailsBase breakpoint in breakpoints)
            {
                Breakpoint psBreakpoint;
                switch (breakpoint)
                {
                    case BreakpointDetails lineBreakpoint:
                        psBreakpoint = SetLineBreakpointDelegate(debugger, lineBreakpoint.Source, lineBreakpoint.LineNumber, lineBreakpoint.ColumnNumber ?? 0, null);
                        break;

                    case CommandBreakpointDetails commandBreakpoint:
                        psBreakpoint = SetCommandBreakpointDelegate(debugger, commandBreakpoint.Name, null, null);
                        break;

                    default:
                        throw new NotImplementedException("Other breakpoints not supported yet");
                }

                psBreakpoints.Add(psBreakpoint);
            }

            return psBreakpoints;
        }

        public static List<Breakpoint> GetBreakpoints(Debugger debugger)
        {
            return GetBreakpointsDelegate(debugger);
        }

        public static bool RemoveBreakpoint(Debugger debugger, Breakpoint breakpoint)
        {
            return RemoveBreakpointDelegate(debugger, breakpoint);
        }

        #endregion
    }
}
