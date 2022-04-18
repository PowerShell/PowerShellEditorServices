// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    using System.Management.Automation;

    internal static class PowerShellExtensions
    {
        private static readonly Action<PowerShell> s_waitForServicingComplete;

        private static readonly Action<PowerShell> s_suspendIncomingData;

        private static readonly Action<PowerShell> s_resumeIncomingData;

        static PowerShellExtensions()
        {
            s_waitForServicingComplete = (Action<PowerShell>)Delegate.CreateDelegate(
                typeof(Action<PowerShell>),
                typeof(PowerShell).GetMethod("WaitForServicingComplete", BindingFlags.Instance | BindingFlags.NonPublic));

            s_suspendIncomingData = (Action<PowerShell>)Delegate.CreateDelegate(
                typeof(Action<PowerShell>),
                typeof(PowerShell).GetMethod("SuspendIncomingData", BindingFlags.Instance | BindingFlags.NonPublic));

            s_resumeIncomingData = (Action<PowerShell>)Delegate.CreateDelegate(
                typeof(Action<PowerShell>),
                typeof(PowerShell).GetMethod("ResumeIncomingData", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        public static PowerShell CloneForNewFrame(this PowerShell pwsh)
        {
            if (pwsh.IsNested)
            {
                return PowerShell.Create(RunspaceMode.CurrentRunspace);
            }

            PowerShell newPwsh = PowerShell.Create();
            newPwsh.Runspace = pwsh.Runspace;
            return newPwsh;
        }

        public static void DisposeWhenCompleted(this PowerShell pwsh)
        {
            static void handler(object self, PSInvocationStateChangedEventArgs e)
            {
                if (e.InvocationStateInfo.State is
                    not PSInvocationState.Completed
                    and not PSInvocationState.Failed
                    and not PSInvocationState.Stopped)
                {
                    return;
                }

                PowerShell pwsh = (PowerShell)self;
                pwsh.InvocationStateChanged -= handler;
                pwsh.Dispose();
            }

            pwsh.InvocationStateChanged += handler;
        }

        public static Collection<TResult> InvokeAndClear<TResult>(this PowerShell pwsh, PSInvocationSettings invocationSettings = null)
        {
            try
            {
                return pwsh.Invoke<TResult>(input: null, invocationSettings);
            }
            finally
            {
                pwsh.Commands.Clear();
            }
        }

        public static void InvokeAndClear(this PowerShell pwsh, PSInvocationSettings invocationSettings = null)
        {
            try
            {
                pwsh.Invoke(input: null, invocationSettings);
            }
            finally
            {
                pwsh.Commands.Clear();
            }
        }

        public static Collection<TResult> InvokeCommand<TResult>(this PowerShell pwsh, PSCommand psCommand, PSInvocationSettings invocationSettings = null)
        {
            pwsh.Commands = psCommand;
            return pwsh.InvokeAndClear<TResult>(invocationSettings);
        }

        public static void InvokeCommand(this PowerShell pwsh, PSCommand psCommand, PSInvocationSettings invocationSettings = null)
        {
            pwsh.Commands = psCommand;
            pwsh.InvokeAndClear(invocationSettings);
        }

        /// <summary>
        /// When running a remote session, waits for remote processing and output to complete.
        /// </summary>
        public static void WaitForRemoteOutputIfNeeded(this PowerShell pwsh)
        {
            if (!pwsh.Runspace.RunspaceIsRemote)
            {
                return;
            }

            // These methods are required when running commands remotely.
            // Remote rendering from command output is done asynchronously.
            // So to ensure we wait for output to be rendered,
            // we need these methods to wait for rendering.
            // PowerShell does this in its own implementation: https://github.com/PowerShell/PowerShell/blob/883ca98dd74ea13b3d8c0dd62d301963a40483d6/src/System.Management.Automation/engine/debugger/debugger.cs#L4628-L4652
            s_waitForServicingComplete(pwsh);
            s_suspendIncomingData(pwsh);
        }

        public static void ResumeRemoteOutputIfNeeded(this PowerShell pwsh)
        {
            if (!pwsh.Runspace.RunspaceIsRemote)
            {
                return;
            }

            s_resumeIncomingData(pwsh);
        }

        public static void SetCorrectExecutionPolicy(this PowerShell pwsh, ILogger logger)
        {
            // We want to get the list hierarchy of execution policies
            // Calling the cmdlet is the simplest way to do that
            IReadOnlyList<PSObject> policies = pwsh
                .AddCommand("Microsoft.PowerShell.Security\\Get-ExecutionPolicy")
                    .AddParameter("-List")
                .InvokeAndClear<PSObject>();

            // The policies come out in the following order:
            // - MachinePolicy
            // - UserPolicy
            // - Process
            // - CurrentUser
            // - LocalMachine
            // We want to ignore policy settings, since we'll already have those anyway.
            // Then we need to look at the CurrentUser setting, and then the LocalMachine setting.
            //
            // Get-ExecutionPolicy -List emits PSObjects with Scope and ExecutionPolicy note properties
            // set to expected values, so we must sift through those.

            ExecutionPolicy policyToSet = ExecutionPolicy.Bypass;
            ExecutionPolicy currentUserPolicy = (ExecutionPolicy)policies[policies.Count - 2].Members["ExecutionPolicy"].Value;
            if (currentUserPolicy != ExecutionPolicy.Undefined)
            {
                policyToSet = currentUserPolicy;
            }
            else
            {
                ExecutionPolicy localMachinePolicy = (ExecutionPolicy)policies[policies.Count - 1].Members["ExecutionPolicy"].Value;
                if (localMachinePolicy != ExecutionPolicy.Undefined)
                {
                    policyToSet = localMachinePolicy;
                }
            }

            // If there's nothing to do, save ourselves a PowerShell invocation
            if (policyToSet == ExecutionPolicy.Bypass)
            {
                logger.LogTrace("Execution policy already set to Bypass. Skipping execution policy set");
                return;
            }

            // Finally set the inherited execution policy
            logger.LogTrace("Setting execution policy to {Policy}", policyToSet);
            try
            {
                pwsh.AddCommand("Microsoft.PowerShell.Security\\Set-ExecutionPolicy")
                    .AddParameter("Scope", ExecutionPolicyScope.Process)
                    .AddParameter("ExecutionPolicy", policyToSet)
                    .AddParameter("Force")
                    .InvokeAndClear();
            }
            catch (CmdletInvocationException e)
            {
                logger.LogError(e, "Error occurred calling 'Set-ExecutionPolicy -Scope Process -ExecutionPolicy {Policy} -Force'", policyToSet);
            }
        }

        public static void LoadProfiles(this PowerShell pwsh, ProfilePathInfo profilePaths)
        {
            // Per the documentation, "the `$PROFILE` variable stores the path to the 'Current User,
            // Current Host' profile. The other profiles are saved in note properties of the
            // `$PROFILE` variable. Its type is `String`.
            //
            // https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_profiles?view=powershell-7.1#the-profile-variable
            PSObject profileVariable = PSObject.AsPSObject(profilePaths.CurrentUserCurrentHost);

            PSCommand psCommand = new PSCommand()
                .AddProfileLoadIfExists(profileVariable, nameof(profilePaths.AllUsersAllHosts), profilePaths.AllUsersAllHosts)
                .AddProfileLoadIfExists(profileVariable, nameof(profilePaths.AllUsersCurrentHost), profilePaths.AllUsersCurrentHost)
                .AddProfileLoadIfExists(profileVariable, nameof(profilePaths.CurrentUserAllHosts), profilePaths.CurrentUserAllHosts)
                .AddProfileLoadIfExists(profileVariable, nameof(profilePaths.CurrentUserCurrentHost), profilePaths.CurrentUserCurrentHost);

            // NOTE: This must be set before the profiles are loaded.
            pwsh.Runspace.SessionStateProxy.SetVariable("PROFILE", profileVariable);

            pwsh.InvokeCommand(psCommand);
        }

        public static void ImportModule(this PowerShell pwsh, string moduleNameOrPath)
        {
            pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                .AddParameter("-Name", moduleNameOrPath)
                .InvokeAndClear();
        }

        public static string GetErrorString(this PowerShell pwsh)
        {
            StringBuilder sb = new StringBuilder(capacity: 1024)
                .AppendLine("Execution of the following command(s) completed with errors:")
                .AppendLine()
                .Append(pwsh.Commands.GetInvocationText());

            sb.AddErrorString(pwsh.Streams.Error[0], errorIndex: 1);
            for (int i = 1; i < pwsh.Streams.Error.Count; i++)
            {
                sb.AppendLine().AppendLine();
                sb.AddErrorString(pwsh.Streams.Error[i], errorIndex: i + 1);
            }

            return sb.ToString();
        }

        private static StringBuilder AddErrorString(this StringBuilder sb, ErrorRecord error, int errorIndex)
        {
            sb.Append("Error #").Append(errorIndex).Append(':').AppendLine()
                .Append(error).AppendLine()
                .AppendLine("ScriptStackTrace:")
                .AppendLine(error.ScriptStackTrace ?? "<null>")
                .AppendLine("Exception:")
                .Append("    ").Append(error.Exception.ToString() ?? "<null>");

            Exception innerException = error.Exception?.InnerException;
            while (innerException != null)
            {
                sb.AppendLine("InnerException:")
                    .Append("    ").Append(innerException);
                innerException = innerException.InnerException;
            }

            return sb;
        }
    }
}
