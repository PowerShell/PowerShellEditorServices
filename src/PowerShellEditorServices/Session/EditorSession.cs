//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Templates;
using Microsoft.PowerShell.EditorServices.Utility;
using System.IO;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Manages a single session for all editor services.  This
    /// includes managing all open script files for the session.
    /// </summary>
    public class EditorSession
    {
        #region Private Fields

        private ILogger logger;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the IHostInput implementation to use for this session.
        /// </summary>
        public IHostInput HostInput { get; private set; }

        /// <summary>
        /// Gets the Workspace instance for this session.
        /// </summary>
        public Workspace Workspace { get; private set; }

        /// <summary>
        /// Gets the PowerShellContext instance for this session.
        /// </summary>
        public PowerShellContext PowerShellContext { get; private set; }

        /// <summary>
        /// Gets the LanguageService instance for this session.
        /// </summary>
        public LanguageService LanguageService { get; private set; }

        /// <summary>
        /// Gets the AnalysisService instance for this session.
        /// </summary>
        public AnalysisService AnalysisService { get; private set; }

        /// <summary>
        /// Gets the DebugService instance for this session.
        /// </summary>
        public DebugService DebugService { get; private set; }

        /// <summary>
        /// Gets the ExtensionService instance for this session.
        /// </summary>
        public ExtensionService ExtensionService { get; private set; }

        /// <summary>
        /// Gets the TemplateService instance for this session.
        /// </summary>
        public TemplateService TemplateService { get; private set; }

        /// <summary>
        /// Gets the RemoteFileManager instance for this session.
        /// </summary>
        public RemoteFileManager RemoteFileManager { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public EditorSession(ILogger logger)
        {
            this.logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        /// <param name="powerShellContext"></param>
        /// <param name="hostInput"></param>
        public void StartSession(
            PowerShellContext powerShellContext,
            IHostInput hostInput)
        {
            this.PowerShellContext = powerShellContext;
            this.HostInput = hostInput;

            // Initialize all services
            this.LanguageService = new LanguageService(this.PowerShellContext, this.logger);
            this.ExtensionService = new ExtensionService(this.PowerShellContext);
            this.TemplateService = new TemplateService(this.PowerShellContext, this.logger);

            this.InstantiateAnalysisService();

            // Create a workspace to contain open files
            this.Workspace = new Workspace(this.PowerShellContext.LocalPowerShellVersion.Version, this.logger);
        }

        /// <summary>
        /// Starts a debug-only session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        /// <param name="powerShellContext"></param>
        /// <param name="editorOperations">
        /// An IEditorOperations implementation used to interact with the editor.
        /// </param>
        public void StartDebugSession(
            PowerShellContext powerShellContext,
            IEditorOperations editorOperations)
        {
            this.PowerShellContext = powerShellContext;

            // Initialize all services
            this.RemoteFileManager = new RemoteFileManager(this.PowerShellContext, editorOperations, logger);
            this.DebugService = new DebugService(this.PowerShellContext, this.RemoteFileManager, logger);

            // Create a workspace to contain open files
            this.Workspace = new Workspace(this.PowerShellContext.LocalPowerShellVersion.Version, this.logger);
        }

        /// <summary>
        /// Starts the DebugService if it's not already strated
        /// </summary>
        /// <param name="editorOperations">
        /// An IEditorOperations implementation used to interact with the editor.
        /// </param>
        public void StartDebugService(IEditorOperations editorOperations)
        {
            if (this.DebugService == null)
            {
                this.RemoteFileManager = new RemoteFileManager(this.PowerShellContext, editorOperations, logger);
                this.DebugService = new DebugService(this.PowerShellContext, this.RemoteFileManager, logger);
            }
        }

        internal void InstantiateAnalysisService(string settingsPath = null)
        {
            // AnalysisService will throw FileNotFoundException if
            // Script Analyzer binaries are not included.
            try
            {
                this.AnalysisService = new AnalysisService(settingsPath, this.logger);
            }
            catch (FileNotFoundException)
            {
                this.logger.Write(
                    LogLevel.Warning,
                    "Script Analyzer binaries not found, AnalysisService will be disabled.");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of any Runspaces that were created for the
        /// services used in this session.
        /// </summary>
        public void Dispose()
        {
            if (this.AnalysisService != null)
            {
                this.AnalysisService.Dispose();
                this.AnalysisService = null;
            }

            if (this.PowerShellContext != null)
            {
                this.PowerShellContext.Dispose();
                this.PowerShellContext = null;
            }
        }

        #endregion
    }
}
