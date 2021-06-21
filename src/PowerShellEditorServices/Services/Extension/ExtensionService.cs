// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services.Extension
{
    /// <summary>
    /// Provides a high-level service which enables PowerShell scripts
    /// and modules to extend the behavior of the host editor.
    /// </summary>
    internal sealed class ExtensionService
    {
        #region Fields

        private readonly Dictionary<string, EditorCommand> editorCommands =
            new Dictionary<string, EditorCommand>();

        private readonly ILanguageServerFacade _languageServer;

        private int _initialized = 0;

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
        internal PowerShellExecutionService ExecutionService { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ExtensionService which uses the provided
        /// PowerShellContext for loading and executing extension code.
        /// </summary>
        /// <param name="languageServer">The PSES language server instance.</param>
        /// <param name="serviceProvider">Services for dependency injection into the editor object.</param>
        /// <param name="editorOptions">Options object to configure the editor.</param>
        /// <param name="executionService">PowerShell execution service to run PowerShell execution requests.</param>
        internal ExtensionService(
            ILanguageServerFacade languageServer,
            IServiceProvider serviceProvider,
            IEditorOperations editorOperations,
            PowerShellExecutionService executionService)
        {
            ExecutionService = executionService;
            _languageServer = languageServer;

            EditorObject =
                new EditorObject(
                    serviceProvider,
                    this,
                    editorOperations);

            // Attach to ExtensionService events
            CommandAdded += ExtensionService_ExtensionAddedAsync;
            CommandUpdated += ExtensionService_ExtensionUpdatedAsync;
            CommandRemoved += ExtensionService_ExtensionRemovedAsync;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes this ExtensionService using the provided IEditorOperations
        /// implementation for future interaction with the host editor.
        /// </summary>
        /// <param name="editorOperations">An IEditorOperations implementation.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        internal Task InitializeAsync()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0)
            {
                return Task.CompletedTask;
            }

            // Assign the new EditorObject to be the static instance available to binary APIs
            EditorObject.SetAsStaticInstance();

            // Register the editor object in the runspace
            return ExecutionService.ExecuteDelegateAsync(
                "Create $psEditorObject",
                ExecutionOptions.Default,
                CancellationToken.None,
                (pwsh, cancellationToken) =>
                {
                    pwsh.Runspace.SessionStateProxy.PSVariable.Set("psEditor", EditorObject);
                });
        }

        /// <summary>
        /// Invokes the specified editor command against the provided EditorContext.
        /// </summary>
        /// <param name="commandName">The unique name of the command to be invoked.</param>
        /// <param name="editorContext">The context in which the command is being invoked.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task InvokeCommandAsync(string commandName, EditorContext editorContext)
        {

            if (this.editorCommands.TryGetValue(commandName, out EditorCommand editorCommand))
            {
                PSCommand executeCommand = new PSCommand();
                executeCommand.AddCommand("Invoke-Command");
                executeCommand.AddParameter("ScriptBlock", editorCommand.ScriptBlock);
                executeCommand.AddParameter("ArgumentList", new object[] { editorContext });

                await ExecutionService.ExecutePSCommandAsync(
                    executeCommand,
                    CancellationToken.None,
                    new PowerShellExecutionOptions { WriteOutputToHost = !editorCommand.SuppressOutput })
                    .ConfigureAwait(false);
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
            Validate.IsNotNull(nameof(editorCommand), editorCommand);

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
            if (this.editorCommands.TryGetValue(commandName, out EditorCommand existingCommand))
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
        /// <returns>An Array of all registered EditorCommands.</returns>
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

        private void ExtensionService_ExtensionAddedAsync(object sender, EditorCommand e)
        {
            _languageServer?.SendNotification<ExtensionCommandAddedNotification>("powerShell/extensionCommandAdded",
                new ExtensionCommandAddedNotification
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName
                });
        }

        private void ExtensionService_ExtensionUpdatedAsync(object sender, EditorCommand e)
        {
            _languageServer?.SendNotification<ExtensionCommandUpdatedNotification>("powerShell/extensionCommandUpdated",
                new ExtensionCommandUpdatedNotification
                {
                    Name = e.Name,
                });
        }

        private void ExtensionService_ExtensionRemovedAsync(object sender, EditorCommand e)
        {
            _languageServer?.SendNotification<ExtensionCommandRemovedNotification>("powerShell/extensionCommandRemoved",
                new ExtensionCommandRemovedNotification
                {
                    Name = e.Name,
                });
        }

        #endregion
    }
}
