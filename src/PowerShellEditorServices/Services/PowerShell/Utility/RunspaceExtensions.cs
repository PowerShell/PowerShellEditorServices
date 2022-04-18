// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    using System.Management.Automation.Runspaces;

    internal static class RunspaceExtensions
    {
        private static readonly Action<Runspace, ApartmentState> s_runspaceApartmentStateSetter;

        private static readonly Func<Runspace, string, string> s_getRemotePromptFunc;

        static RunspaceExtensions()
        {
            // PowerShell ApartmentState APIs aren't available in PSStandard, so we need to use reflection.
            MethodInfo setterInfo = typeof(Runspace).GetProperty("ApartmentState").GetSetMethod();
            Delegate setter = Delegate.CreateDelegate(typeof(Action<Runspace, ApartmentState>), firstArgument: null, method: setterInfo);
            s_runspaceApartmentStateSetter = (Action<Runspace, ApartmentState>)setter;

            MethodInfo getRemotePromptMethod = typeof(HostUtilities).GetMethod("GetRemotePrompt", BindingFlags.NonPublic | BindingFlags.Static);
            ParameterExpression runspaceParam = Expression.Parameter(typeof(Runspace));
            ParameterExpression basePromptParam = Expression.Parameter(typeof(string));
            s_getRemotePromptFunc = Expression.Lambda<Func<Runspace, string, string>>(
                Expression.Call(
                    getRemotePromptMethod,
                    new Expression[]
                    {
                        Expression.Convert(runspaceParam, typeof(Runspace).Assembly.GetType("System.Management.Automation.RemoteRunspace")),
                        basePromptParam,
                        Expression.Constant(false), // configuredSession must be false
                    }),
                new ParameterExpression[] { runspaceParam, basePromptParam }).Compile();
        }

        public static void SetApartmentStateToSta(this Runspace runspace) => s_runspaceApartmentStateSetter?.Invoke(runspace, ApartmentState.STA);

        /// <summary>
        /// Augment a given prompt string with a remote decoration.
        /// This is an internal method on <c>Runspace</c> in PowerShell that we reuse via reflection.
        /// </summary>
        /// <param name="runspace">The runspace the prompt is for.</param>
        /// <param name="basePrompt">The base prompt to decorate.</param>
        /// <returns>A prompt string decorated with remote connection details.</returns>
        public static string GetRemotePrompt(this Runspace runspace, string basePrompt) => s_getRemotePromptFunc(runspace, basePrompt);

        public static void ThrowCancelledIfUnusable(this Runspace runspace)
            => runspace.RunspaceStateInfo.ThrowCancelledIfUnusable();

        public static void ThrowCancelledIfUnusable(this RunspaceStateInfo runspaceStateInfo)
        {
            if (!IsUsable(runspaceStateInfo))
            {
                throw new OperationCanceledException();
            }
        }

        public static bool IsUsable(this RunspaceStateInfo runspaceStateInfo)
        {
            return runspaceStateInfo.State switch
            {
                RunspaceState.Broken or RunspaceState.Closed or RunspaceState.Closing or RunspaceState.Disconnecting or RunspaceState.Disconnected => false,
                _ => true,
            };
        }
    }
}
