//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides the entry point of the extensibility API, inserted into
    /// the PowerShell session as the "$psEditor" variable.
    /// </summary>
    public class EditorObject
    {
        #region Private Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ExtensionService _extensionService;
        private readonly IEditorOperations _editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the version of PowerShell Editor Services.
        /// </summary>
        public Version EditorServicesVersion
        {
            get { return this.GetType().GetTypeInfo().Assembly.GetName().Version; }
        }

        /// <summary>
        /// Gets the workspace interface for the editor API.
        /// </summary>
        public EditorWorkspace Workspace { get; private set; }

        /// <summary>
        /// Gets the window interface for the editor API.
        /// </summary>
        public EditorWindow Window { get; private set; }

        /// <summary>
        /// Gets the components that are registered.
        /// </summary>
        public IServiceProvider Components => _serviceProvider;

        #endregion

        /// <summary>
        /// Creates a new instance of the EditorObject class.
        /// </summary>
        /// <param name="extensionService">An ExtensionService which handles command registration.</param>
        /// <param name="editorOperations">An IEditorOperations implementation which handles operations in the host editor.</param>
        public EditorObject(
            IServiceProvider serviceProvider,
            ExtensionService extensionService,
            IEditorOperations editorOperations)
        {
            this._serviceProvider = serviceProvider;
            this._extensionService = extensionService;
            this._editorOperations = editorOperations;

            // Create API area objects
            this.Workspace = new EditorWorkspace(this._editorOperations);
            this.Window = new EditorWindow(this._editorOperations);
        }

        /// <summary>
        /// Registers a new command in the editor.
        /// </summary>
        /// <param name="editorCommand">The EditorCommand to be registered.</param>
        /// <returns>True if the command is newly registered, false if the command already exists.</returns>
        public bool RegisterCommand(EditorCommand editorCommand)
        {
            return this._extensionService.RegisterCommand(editorCommand);
        }

        /// <summary>
        /// Unregisters an existing EditorCommand based on its registered name.
        /// </summary>
        /// <param name="commandName">The name of the command to be unregistered.</param>
        public void UnregisterCommand(string commandName)
        {
            this._extensionService.UnregisterCommand(commandName);
        }

        /// <summary>
        /// Returns all registered EditorCommands.
        /// </summary>
        /// <returns>An Array of all registered EditorCommands.</returns>
        public EditorCommand[] GetCommands()
        {
            return this._extensionService.GetCommands();
        }
        /// <summary>
        /// Gets the EditorContext which contains the state of the editor
        /// at the time this method is invoked.
        /// </summary>
        /// <returns>A instance of the EditorContext class.</returns>
        public EditorContext GetEditorContext()
        {
            return this._editorOperations.GetEditorContextAsync().Result;
        }

        /// <summary>
        /// Get's the desired service which allows for advanced control of PowerShellEditorServices.
        /// </summary>
        /// <returns>The singleton service object of the type requested.</returns>
        public object GetService(Type type)
        {
            return _serviceProvider.GetService(type);
        }
    }
}
