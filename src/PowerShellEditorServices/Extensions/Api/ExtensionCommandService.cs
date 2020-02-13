//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions.Services
{
    /// <summary>
    /// Service for registration and invocation of extension commands.
    /// </summary>
    public interface IExtensionCommandService
    {
        /// <summary>
        /// Invoke an extension command asynchronously.
        /// </summary>
        /// <param name="commandName">The name of the extension command to invoke.</param>
        /// <param name="editorContext">The editor context in which to invoke the command.</param>
        /// <returns>A task that resolves when the command has been run.</returns>
        Task InvokeCommandAsync(string commandName, EditorContext editorContext);

        /// <summary>
        /// Registers a new EditorCommand with the ExtensionService and
        /// causes its details to be sent to the host editor.
        /// </summary>
        /// <param name="editorCommand">The details about the editor command to be registered.</param>
        /// <returns>True if the command is newly registered, false if the command already exists.</returns>
        bool RegisterCommand(EditorCommand editorCommand);

        /// <summary>
        /// Unregisters an existing EditorCommand based on its registered name.
        /// </summary>
        /// <param name="commandName">The name of the command to be unregistered.</param>
        void UnregisterCommand(string commandName);

        /// <summary>
        /// Returns all registered EditorCommands.
        /// </summary>
        /// <returns>A list of all registered EditorCommands.</returns>
        IReadOnlyList<EditorCommand> GetCommands();

        /// <summary>
        /// Raised when a new editor command is added.
        /// </summary>
        event EventHandler<EditorCommand> CommandAdded;

        /// <summary>
        /// Raised when an existing editor command is updated.
        /// </summary>
        event EventHandler<EditorCommand> CommandUpdated;

        /// <summary>
        /// Raised when an existing editor command is removed.
        /// </summary>
        event EventHandler<EditorCommand> CommandRemoved;
    }

    internal class ExtensionCommandService : IExtensionCommandService
    {
        private readonly ExtensionService _extensionService;

        public ExtensionCommandService(ExtensionService extensionService)
        {
            _extensionService = extensionService;

            _extensionService.CommandAdded += OnCommandAdded;
            _extensionService.CommandUpdated += OnCommandUpdated;
            _extensionService.CommandRemoved += OnCommandRemoved;
        }

        public event EventHandler<EditorCommand> CommandAdded;

        public event EventHandler<EditorCommand> CommandUpdated;

        public event EventHandler<EditorCommand> CommandRemoved;

        public IReadOnlyList<EditorCommand> GetCommands() => _extensionService.GetCommands();

        public Task InvokeCommandAsync(string commandName, EditorContext editorContext) => _extensionService.InvokeCommandAsync(commandName, editorContext);

        public bool RegisterCommand(EditorCommand editorCommand) => _extensionService.RegisterCommand(editorCommand);

        public void UnregisterCommand(string commandName) => _extensionService.UnregisterCommand(commandName);

        private void OnCommandAdded(object sender, EditorCommand editorCommand)
        {
            CommandAdded?.Invoke(this, editorCommand);
        }

        private void OnCommandUpdated(object sender, EditorCommand editorCommand)
        {
            CommandUpdated?.Invoke(this, editorCommand);
        }

        private void OnCommandRemoved(object sender, EditorCommand editorCommand)
        {
            CommandRemoved?.Invoke(this, editorCommand);
        }
    }
}
