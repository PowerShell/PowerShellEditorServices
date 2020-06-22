using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class RunspaceExtensions
    {
        private static readonly Action<Runspace, ApartmentState> s_runspaceApartmentStateSetter;

        static RunspaceExtensions()
        {
            // PowerShell ApartmentState APIs aren't available in PSStandard, so we need to use reflection.
            if (!VersionUtils.IsNetCore || VersionUtils.IsPS7OrGreater)
            {
                MethodInfo setterInfo = typeof(Runspace).GetProperty("ApartmentState").GetSetMethod();
                Delegate setter = Delegate.CreateDelegate(typeof(Action<Runspace, ApartmentState>), firstArgument: null, method: setterInfo);
                s_runspaceApartmentStateSetter = (Action<Runspace, ApartmentState>)setter;
            }
        }

        public static void SetApartmentStateToSta(this Runspace runspace)
        {
            s_runspaceApartmentStateSetter?.Invoke(runspace, ApartmentState.STA);
        }
    }
}
