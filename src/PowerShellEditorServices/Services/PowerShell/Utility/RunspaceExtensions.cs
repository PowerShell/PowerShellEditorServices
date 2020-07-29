using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class RunspaceExtensions
    {
        private static readonly Action<Runspace, ApartmentState> s_runspaceApartmentStateSetter;

        private static readonly Func<Runspace, string, string> s_getRemotePromptFunc;

        static RunspaceExtensions()
        {
            // PowerShell ApartmentState APIs aren't available in PSStandard, so we need to use reflection.
            if (!VersionUtils.IsNetCore || VersionUtils.IsPS7OrGreater)
            {
                MethodInfo setterInfo = typeof(Runspace).GetProperty("ApartmentState").GetSetMethod();
                Delegate setter = Delegate.CreateDelegate(typeof(Action<Runspace, ApartmentState>), firstArgument: null, method: setterInfo);
                s_runspaceApartmentStateSetter = (Action<Runspace, ApartmentState>)setter;
            }

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

        public static void SetApartmentStateToSta(this Runspace runspace)
        {
            s_runspaceApartmentStateSetter?.Invoke(runspace, ApartmentState.STA);
        }

        public static string GetRemotePrompt(this Runspace runspace, string basePrompt)
        {
            return s_getRemotePromptFunc(runspace, basePrompt);
        }
    }
}
