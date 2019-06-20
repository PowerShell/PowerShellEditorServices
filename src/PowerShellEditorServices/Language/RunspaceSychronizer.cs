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
    /// Does a thing
    /// </summary>
    public class RunspaceSynchronizer
    {
        /// <summary>
        /// Does a thing
        /// </summary>
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

        private static List<PSModuleInfo> moduleToImport = new List<PSModuleInfo>();
        private static List<PSVariable> variablesToSet = new List<PSVariable>();

        private static Runspace sourceRunspace;
        private static Runspace targetRunspace;
        private static EngineIntrinsics sourceEngineIntrinsics;
        private static EngineIntrinsics targetEngineIntrinsics;

        private static object syncObj = new object();

        /// <summary>
        /// Does a thing
        /// </summary>
        public static bool IsReadyForEvents { get; private set; }

        private static void HandleRunspaceStateChange(object sender, PSEventArgs args)
        {
            if (!SourceActionEnabled)
            {
                return;
            }

            SourceActionEnabled = false;

            try
            {
                // Maybe also track the latest history item id ($h = Get-History -Count 1; $h.Id)
                // to make sure we do the collection only if there was actually any input.

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
                    // TODO: first filter out the built-in variables.
                    if(!variableCache.TryGetValue(variable.Name, out Type value) || value != variable.Value?.GetType())
                    {
                        variableCache[variable.Name] = variable.Value?.GetType();

                        newOrChangedVars.Add(variable);
                    }
                }

                if (newOrChangedModules.Count == 0 && newOrChangedVars.Count == 0)
                {
                    return;
                }

                lock (syncObj)
                {
                    moduleToImport.AddRange(newOrChangedModules);
                    variablesToSet.AddRange(newOrChangedVars);
                }

                // Enable the action in target Runspace
                UpdateTargetRunspaceState();
            } catch (Exception ex) {
                System.Console.WriteLine(ex.Message);
                System.Console.WriteLine(ex.StackTrace);
            }
        }

        private static void UpdateTargetRunspaceState()
        {
            List<PSModuleInfo> newOrChangedModules;
            List<PSVariable> newOrChangedVars;

            lock (syncObj)
            {
                newOrChangedModules = new List<PSModuleInfo>(moduleToImport);
                newOrChangedVars = new List<PSVariable>(variablesToSet);

                moduleToImport.Clear();
                variablesToSet.Clear();
            }

            if (newOrChangedModules.Count > 0)
            {
                // Import the modules with -Force
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

        /// <summary>
        /// Does a thing
        /// </summary>
        public static void InitializeRunspaces(Runspace runspaceSource, Runspace runspaceTarget)
        {
            sourceRunspace = runspaceSource;
            sourceEngineIntrinsics = ReflectionUtils.GetEngineIntrinsics(sourceRunspace);
            IsReadyForEvents = true;

            targetRunspace = runspaceTarget;
            targetEngineIntrinsics = ReflectionUtils.GetEngineIntrinsics(runspaceTarget);

            if(sourceEngineIntrinsics != null)
            {
                sourceEngineIntrinsics.Events.SubscribeEvent(
                    source: null,
                    eventName: null,
                    sourceIdentifier: PSEngineEvent.OnIdle.ToString(),
                    data: null,
                    handlerDelegate: HandleRunspaceStateChange,
                    supportEvent: true,
                    forwardEvent: false);
            }

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

        internal class ReflectionUtils
        {
            private static BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default;

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

    internal static class EngineIntrinsicsExtensions
    {
        internal static List<PSVariable> GetVariables(this EngineIntrinsics engineIntrinsics)
        {
            List<PSVariable> variables = new List<PSVariable>();
            foreach (PSObject psobject in engineIntrinsics.GetItems(ItemType.Variable))
            {
                var variable = (PSVariable) psobject.BaseObject;
                variables.Add(variable);
            }
            return variables;
        }

        internal static void SetVariable(this EngineIntrinsics engineIntrinsics, PSVariable variable)
        {
            engineIntrinsics.SetItem(ItemType.Variable, variable.Name, variable.Value);
        }

        private static Collection<PSObject> GetItems(this EngineIntrinsics engineIntrinsics, ItemType itemType)
        {
            for (int i = 0; i < 3; i++)
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

        private static void SetItem(this EngineIntrinsics engineIntrinsics, ItemType itemType, string name, object value)
        {
            for (int i = 0; i < 3; i++)
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

        private enum ItemType
        {
            Variable,
            Function,
            Alias
        }
    }
}
