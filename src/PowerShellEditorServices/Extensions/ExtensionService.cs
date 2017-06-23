//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Components;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a high-level service which enables PowerShell scripts
    /// and modules to extend the behavior of the host editor.
    /// </summary>
    public class ExtensionService
    {
        #region Fields

        private Dictionary<string, EditorCommand> editorCommands =
            new Dictionary<string, EditorCommand>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the IEditorOperations implementation used to invoke operations
        /// in the host editor.
        /// </summary>
        public IEditorOperations EditorOperations { get; private set; }

        /// <summary>
        /// Gets the EditorObject which exists in the PowerShell session as the
        /// '$psEditor' variable.
        /// </summary>
        public EditorObject EditorObject { get; private set; }

        /// <summary>
        /// Gets the PowerShellContext in which extension code will be executed.
        /// </summary>
        public PowerShellContext PowerShellContext { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ExtensionService which uses the provided
        /// PowerShellContext for loading and executing extension code.
        /// </summary>
        /// <param name="powerShellContext">A PowerShellContext used to execute extension code.</param>
        public ExtensionService(PowerShellContext powerShellContext)
        {
            this.PowerShellContext = powerShellContext;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes this ExtensionService using the provided IEditorOperations
        /// implementation for future interaction with the host editor.
        /// </summary>
        /// <param name="editorOperations">An IEditorOperations implementation.</param>
        /// <param name="componentRegistry">An IComponentRegistry instance which provides components in the session.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task Initialize(
            IEditorOperations editorOperations,
            IComponentRegistry componentRegistry)
        {
            this.EditorObject =
                new EditorObject(
                    this,
                    editorOperations,
                    componentRegistry);

            // Register the editor object in the runspace
            PSCommand variableCommand = new PSCommand();
            using (RunspaceHandle handle = await this.PowerShellContext.GetRunspaceHandle())
            {
                handle.Runspace.SessionStateProxy.PSVariable.Set(
                    "psEditor",
                    this.EditorObject);
            }
        }

        /// <summary>
        /// Invokes the specified editor command against the provided EditorContext.
        /// </summary>
        /// <param name="commandName">The unique name of the command to be invoked.</param>
        /// <param name="editorContext">The context in which the command is being invoked.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task InvokeCommand(string commandName, EditorContext editorContext)
        {
            EditorCommand editorCommand;

            if (this.editorCommands.TryGetValue(commandName, out editorCommand))
            {
                PSCommand executeCommand = new PSCommand();
                executeCommand.AddCommand("Invoke-Command");
                executeCommand.AddParameter("ScriptBlock", editorCommand.ScriptBlock);
                executeCommand.AddParameter("ArgumentList", new object[] { editorContext });

                await this.PowerShellContext.ExecuteCommand<object>(
                    executeCommand,
                    !editorCommand.SuppressOutput,
                    true);
            }
            else
            {
                throw new KeyNotFoundException(
                    string.Format(
                        "Editor command not found: '{0}'",
                        commandName));
            }
        }

        /// <summary>
        /// Registers a new EditorCommand with the ExtensionService and
        /// causes its details to be sent to the host editor.
        /// </summary>
        /// <param name="editorCommand">The details about the editor command to be registered.</param>
        /// <returns>True if the command is newly registered, false if the command already exists.</returns>
        public bool RegisterCommand(EditorCommand editorCommand)
        {
            bool commandExists =
                this.editorCommands.ContainsKey(
                    editorCommand.Name);

            // Add or replace the editor command
            this.editorCommands[editorCommand.Name] = editorCommand;

            if (!commandExists)
            {
                this.OnCommandAdded(editorCommand);
            }
            else
            {
                this.OnCommandUpdated(editorCommand);
            }

            return !commandExists;
        }

        /// <summary>
        /// Unregisters an existing EditorCommand based on its registered name.
        /// </summary>
        /// <param name="commandName">The name of the command to be unregistered.</param>
        public void UnregisterCommand(string commandName)
        {
            EditorCommand existingCommand = null;
            if (this.editorCommands.TryGetValue(commandName, out existingCommand))
            {
                this.editorCommands.Remove(commandName);
                this.OnCommandRemoved(existingCommand);
            }
            else
            {
                throw new KeyNotFoundException(
                    string.Format(
                        "Command '{0}' is not registered",
                        commandName));
            }
        }

        /// <summary>
        /// Returns all registered EditorCommands.
        /// </summary>
        /// <returns>An Array of all registered EditorCommands.</return>
        public EditorCommand[] GetCommands()
        {
            EditorCommand[] commands = new EditorCommand[this.editorCommands.Count];
            this.editorCommands.Values.CopyTo(commands,0);
            return commands;
        }
        #endregion

        #region Events

        /// <summary>
        /// Raised when a new editor command is added.
        /// </summary>
        public event EventHandler<EditorCommand> CommandAdded;

        private void OnCommandAdded(EditorCommand command)
        {
            this.CommandAdded?.Invoke(this, command);
        }

        /// <summary>
        /// Raised when an existing editor command is updated.
        /// </summary>
        public event EventHandler<EditorCommand> CommandUpdated;

        private void OnCommandUpdated(EditorCommand command)
        {
            this.CommandUpdated?.Invoke(this, command);
        }

        /// <summary>
        /// Raised when an existing editor command is removed.
        /// </summary>
        public event EventHandler<EditorCommand> CommandRemoved;

        private void OnCommandRemoved(EditorCommand command)
        {
            this.CommandRemoved?.Invoke(this, command);
        }

        #endregion
    }
}

