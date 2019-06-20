//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices
{
    using System.Management.Automation;

    /// <summary>
    /// This class is used to sync the state of one runspace to another.
    /// It's done by copying over variables and reimporting modules into the target runspace.
    /// It doesn't rely on the pipeline of the source runspace at all, instead leverages Reflection
    /// to access internal properties and methods on the Runspace type.
    /// Lastly, in order to trigger the synchronizing, you must call the Activate method. This will go
    /// in the PSReadLine key handler for ENTER.
    /// </summary>
    public class RunspaceSynchronizer
    {
        // Determines whether the HandleRunspaceStateChange event should attempt to sync the runspaces.
        private static bool SourceActionEnabled = false;

        // 'moduleCache' keeps track of all modules imported in the source Runspace.
        // when there is a `Import-Module -Force`, the new module object would be a
        // different instance with different hashcode, so we can tell if there is a
        // force loading of an already loaded module.
        private static HashSet<PSModuleInfo> moduleCache = new HashSet<PSModuleInfo>();

        // 'variableCache' keeps all global scope variable names and their value type.
        // As long as the value type doesn't change, we don't need to update the variable
        // in the target Runspace, because all tab completion needs is the type information.
        private static Dictionary<string, Type> variableCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private static Runspace sourceRunspace;
        private static Runspace targetRunspace;
        private static EngineIntrinsics sourceEngineIntrinsics;
        private static EngineIntrinsics targetEngineIntrinsics;

        private readonly static HashSet<string> POWERSHELL_MAGIC_VARIABLES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PID",
            "PSVersionTable",
            "PSEdition",
            "PSHOME",
            "HOST",
            "true",
            "false",
            "null",
            "Error",
            "IsMacOS",
            "IsLinux",
            "IsWindows"
        };

        /// <summary>
        /// Determines if the RunspaceSynchronizer has been initialized.
        /// </summary>
        public static bool IsReadyForEvents { get; private set; }

        #region Public methods

        /// <summary>
        /// Does a thing
        /// </summary>
        public static void InitializeRunspaces(Runspace runspaceSource, Runspace runspaceTarget)
        {
            sourceRunspace = runspaceSource;
            sourceEngineIntrinsics = ReflectionUtils.GetEngineIntrinsics(sourceRunspace);
            targetRunspace = runspaceTarget;
            targetEngineIntrinsics = ReflectionUtils.GetEngineIntrinsics(runspaceTarget);
            IsReadyForEvents = true;

            sourceEngineIntrinsics.Events.SubscribeEvent(
                source: null,
                eventName: null,
                sourceIdentifier: PSEngineEvent.OnIdle.ToString(),
                data: null,
                handlerDelegate: HandleRunspaceStateChange,
                supportEvent: true,
                forwardEvent: false);

            Activate();
            // Trigger events
            HandleRunspaceStateChange(sender: null, args: null);
        }

        /// <summary>
        /// Does a thing
        /// </summary>
        public static void Activate()
        {
            SourceActionEnabled = true;
        }

        #endregion

        #region Private Methods

        private static void HandleRunspaceStateChange(object sender, PSEventArgs args)
        {
            if (!SourceActionEnabled)
            {
                return;
            }

            SourceActionEnabled = false;

            var newOrChangedModules = new List<PSModuleInfo>();
            List<PSModuleInfo> modules = ReflectionUtils.GetModules(sourceRunspace);
            foreach (PSModuleInfo module in modules)
            {
                if (moduleCache.Add(module))
                {
                    newOrChangedModules.Add(module);
                }
            }


            var newOrChangedVars = new List<PSVariable>();

            var variables = sourceEngineIntrinsics.GetVariables();
            foreach (var variable in variables)
            {
                // If the variable is a magic variable or it's type has not changed, then skip it.
                if(POWERSHELL_MAGIC_VARIABLES.Contains(variable.Name) ||
                    (variableCache.TryGetValue(variable.Name, out Type value) && value == variable.Value?.GetType()))
                {
                    continue;
                }

                // Add the variable to the cache and mark it as a newOrChanged variable.
                variableCache[variable.Name] = variable.Value?.GetType();
                newOrChangedVars.Add(variable);
            }

            if (newOrChangedModules.Count > 0)
            {
                // Import the modules in the targetRunspace with -Force
                using (PowerShell pwsh = PowerShell.Create())
                {
                    pwsh.Runspace = targetRunspace;

                    foreach (PSModuleInfo moduleInfo in newOrChangedModules)
                    {
                        if(moduleInfo.Path != null)
                        {
                            pwsh.AddCommand("Import-Module")
                                .AddParameter("Name", moduleInfo.Path)
                                .AddParameter("Force")
                                .AddStatement();
                        }
                    }

                    pwsh.Invoke();
                }
            }

            if (newOrChangedVars.Count > 0)
            {
                // Set or update the variables.
                foreach (PSVariable variable in newOrChangedVars)
                {
                    targetEngineIntrinsics.SetVariable(variable);
                }
            }
        }

        #endregion

        // A collection of helper methods that use Reflection in some form.
        private class ReflectionUtils
        {
            private static BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default;

            // Gets the modules loaded in a runspace.
            // This exists in runspace.ExecutionContext.Modules.GetModule(string[] patterns, bool all)
            internal static List<PSModuleInfo> GetModules(Runspace runspace)
            {
                var executionContext = typeof(Runspace)
                    .GetProperty("ExecutionContext", bindingFlags)
                    .GetValue(runspace);
                var ModuleIntrinsics = executionContext.GetType()
                    .GetProperty("Modules", bindingFlags)
                    .GetValue(executionContext);
                var modules = ModuleIntrinsics.GetType()
                    .GetMethod("GetModules", bindingFlags, null, new Type[] { typeof(string[]), typeof(bool) }, null)
                    .Invoke(ModuleIntrinsics, new object[] { new string[] { "*" }, false }) as List<PSModuleInfo>;
                return modules;
            }

            // Gets the engine intrinsics object on a Runspace.
            // This exists in runspace.ExecutionContext.EngineIntrinsics.
            internal static EngineIntrinsics GetEngineIntrinsics(Runspace runspace)
            {
                var executionContext = typeof(Runspace)
                    .GetProperty("ExecutionContext", bindingFlags)
                    .GetValue(runspace);
                var engineIntrinsics = executionContext.GetType()
                    .GetProperty("EngineIntrinsics", bindingFlags)
                    .GetValue(executionContext) as EngineIntrinsics;
                return engineIntrinsics;
            }
        }
    }

    // Extension methods on EngineIntrinsics to streamline some setters and setters.
    internal static class EngineIntrinsicsExtensions
    {
        private const int RETRY_ATTEMPTS = 3;
        internal static List<PSVariable> GetVariables(this EngineIntrinsics engineIntrinsics)
        {
            List<PSVariable> variables = new List<PSVariable>();
            foreach (PSObject psobject in engineIntrinsics.GetItems(ItemProviderType.Variable))
            {
                var variable = (PSVariable) psobject.BaseObject;
                variables.Add(variable);
            }
            return variables;
        }

        internal static void SetVariable(this EngineIntrinsics engineIntrinsics, PSVariable variable)
        {
            engineIntrinsics.SetItem(ItemProviderType.Variable, variable.Name, variable.Value);
        }

        private static Collection<PSObject> GetItems(this EngineIntrinsics engineIntrinsics, ItemProviderType itemType)
        {
            for (int i = 0; i < RETRY_ATTEMPTS; i++)
            {
                try
                {
                    return engineIntrinsics.InvokeProvider.Item.Get($@"{itemType.ToString()}:\*");
                }
                catch(Exception)
                {
                    // InvokeProvider.Item.Get is not threadsafe so let's try a couple times
                    // to get results from it.
                }
            }
            return new Collection<PSObject>();
        }

        private static void SetItem(this EngineIntrinsics engineIntrinsics, ItemProviderType itemType, string name, object value)
        {
            for (int i = 0; i < RETRY_ATTEMPTS; i++)
            {
                try
                {
                    engineIntrinsics.InvokeProvider.Item.Set($@"{itemType}:\{name}", value);
                    return;
                }
                catch (Exception)
                {
                    // InvokeProvider.Item.Set is not threadsafe so let's try a couple times to set.
                }
            }
        }

        private enum ItemProviderType
        {
            Variable,
            Function,
            Alias
        }
    }
}
