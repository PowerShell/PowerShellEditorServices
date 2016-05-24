//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides the entry point of the extensibility API, inserted into
    /// the PowerShell session as the "$psEditor" variable.
    /// </summary>
    public class EditorObject
    {
        #region Private Fields

        private ExtensionService extensionService;
        private IEditorOperations editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the version of PowerShell Editor Services.
        /// </summary>
        public Version EditorServicesVersion
        {
            get { return this.GetType().Assembly.GetName().Version; }
        }

        /// <summary>
        /// Gets the workspace interface for the editor API.
        /// </summary>
        public EditorWorkspace Workspace { get; private set; }

        /// <summary>
        /// Gets the window interface for the editor API.
        /// </summary>
        public EditorWindow Window { get; private set; }

        #endregion

        /// <summary>
        /// Creates a new instance of the EditorObject class.
        /// </summary>
        /// <param name="extensionService">An ExtensionService which handles command registration.</param>
        /// <param name="editorOperations">An IEditorOperations implementation which handles operations in the host editor.</param>
        public EditorObject(
            ExtensionService extensionService,
            IEditorOperations editorOperations)
        {
            this.extensionService = extensionService;
            this.editorOperations = editorOperations;

            // Create API area objects
            this.Workspace = new EditorWorkspace(this.editorOperations);
            this.Window = new EditorWindow(this.editorOperations);
        }

        /// <summary>
        /// Registers a new command in the editor.
        /// </summary>
        /// <param name="editorCommand">The EditorCommand to be registered.</param>
        /// <returns>True if the command is newly registered, false if the command already exists.</returns>
        public bool RegisterCommand(EditorCommand editorCommand)
        {
            return this.extensionService.RegisterCommand(editorCommand);
        }

        /// <summary>
        /// Unregisters an existing EditorCommand based on its registered name.
        /// </summary>
        /// <param name="commandName">The name of the command to be unregistered.</param>
        public void UnregisterCommand(string commandName)
        {
            this.extensionService.UnregisterCommand(commandName);
        }

        /// <summary>
        /// Gets the EditorContext which contains the state of the editor
        /// at the time this method is invoked.
        /// </summary>
        /// <returns>A instance of the EditorContext class.</returns>
        public EditorContext GetEditorContext()
        {
            return this.editorOperations.GetEditorContext().Result;
        }
    }
}

