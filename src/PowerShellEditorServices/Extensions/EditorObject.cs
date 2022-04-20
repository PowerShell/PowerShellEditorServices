// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Extension class to access the editor API with.
    /// This is done so that the async/ALC APIs aren't exposed to PowerShell, where they're likely only to cause problems.
    /// </summary>
    public static class EditorObjectExtensions
    {
        /// <summary>
        /// Get the provider of extension services for .NET extension tooling.
        /// </summary>
        /// <param name="editorObject">The editor object ($psEditor).</param>
        /// <returns>The extension services provider.</returns>
        public static EditorExtensionServiceProvider GetExtensionServiceProvider(this EditorObject editorObject) => editorObject.Api;
    }

    /// <summary>
    /// Provides the entry point of the extensibility API, inserted into
    /// the PowerShell session as the "$psEditor" variable.
    /// </summary>
    public class EditorObject
    {
        private static readonly TaskCompletionSource<bool> s_editorObjectReady = new();

        /// <summary>
        /// A reference to the editor object instance. Only valid when <see cref="EditorObjectReady"/> completes.
        /// </summary>
        public static EditorObject Instance { get; private set; }

        /// <summary>
        /// A task that completes when the editor object static instance has been set.
        /// </summary>
        public static Task EditorObjectReady => s_editorObjectReady.Task;

        #region Private Fields

        private readonly ExtensionService _extensionService;
        private readonly IEditorOperations _editorOperations;
        private readonly Lazy<EditorExtensionServiceProvider> _apiLazy;

        #endregion

        #region Properties

        internal EditorExtensionServiceProvider Api => _apiLazy.Value;

        /// <summary>
        /// Gets the version of PowerShell Editor Services.
        /// </summary>
        public Version EditorServicesVersion => GetType().GetTypeInfo().Assembly.GetName().Version;

        /// <summary>
        /// Gets the workspace interface for the editor API.
        /// </summary>
        public EditorWorkspace Workspace { get; }

        /// <summary>
        /// Gets the window interface for the editor API.
        /// </summary>
        public EditorWindow Window { get; }

        #endregion

        /// <summary>
        /// Creates a new instance of the EditorObject class.
        /// </summary>
        /// <param name="serviceProvider">The service provider?</param>
        /// <param name="extensionService">An ExtensionService which handles command registration.</param>
        /// <param name="editorOperations">An IEditorOperations implementation which handles operations in the host editor.</param>
        internal EditorObject(
            IServiceProvider serviceProvider,
            ExtensionService extensionService,
            IEditorOperations editorOperations)
        {
            _extensionService = extensionService;
            _editorOperations = editorOperations;

            // Create API area objects
            Workspace = new EditorWorkspace(_editorOperations);
            Window = new EditorWindow(_editorOperations);

            // Create this lazily so that dependency injection does not have a circular call dependency
            _apiLazy = new Lazy<EditorExtensionServiceProvider>(() => new EditorExtensionServiceProvider(serviceProvider));
        }

        /// <summary>
        /// Registers a new command in the editor.
        /// </summary>
        /// <param name="editorCommand">The EditorCommand to be registered.</param>
        /// <returns>True if the command is newly registered, false if the command already exists.</returns>
        public bool RegisterCommand(EditorCommand editorCommand) => _extensionService.RegisterCommand(editorCommand);

        /// <summary>
        /// Unregisters an existing EditorCommand based on its registered name.
        /// </summary>
        /// <param name="commandName">The name of the command to be unregistered.</param>
        public void UnregisterCommand(string commandName) => _extensionService.UnregisterCommand(commandName);

        /// <summary>
        /// Returns all registered EditorCommands.
        /// </summary>
        /// <returns>An Array of all registered EditorCommands.</returns>
        public EditorCommand[] GetCommands() => _extensionService.GetCommands();
        /// <summary>
        /// Gets the EditorContext which contains the state of the editor
        /// at the time this method is invoked.
        /// </summary>
        /// <returns>A instance of the EditorContext class.</returns>
        public EditorContext GetEditorContext() => _editorOperations.GetEditorContextAsync().Result;

        internal void SetAsStaticInstance()
        {
            EditorObject.Instance = this;
            s_editorObjectReady.TrySetResult(true);
        }
    }
}
