//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Manages a single session for all editor services.  This 
    /// includes managing all open script files for the session.
    /// </summary>
    public class EditorSession
    {
        #region Properties

        /// <summary>
        /// Gets the Workspace instance for this session.
        /// </summary>
        public Workspace Workspace { get; private set; }

        /// <summary>
        /// Gets the PowerShellSession instance for this session.
        /// </summary>
        public PowerShellSession PowerShellSession { get; private set; }

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

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        /// <param name="consoleHost">
        /// An IConsoleHost implementation which is used to interact with the
        /// host's user interface.
        /// </param>
        public void StartSession(IConsoleHost consoleHost)
        {
            // Create a workspace to contain open files
            this.Workspace = new Workspace();

            // Create a runspace to share between the language and analysis services
            // TODO: Do this somewhere else!
            Runspace languageRunspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
            languageRunspace.ApartmentState = ApartmentState.STA;
            languageRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
            languageRunspace.Open();
            languageRunspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);

            // Initialize all services
            this.PowerShellSession = new PowerShellSession();
            this.LanguageService = new LanguageService(this.PowerShellSession);
            this.AnalysisService = new AnalysisService(languageRunspace);
            this.DebugService = new DebugService(this.PowerShellSession);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of any Runspaces that were created for the
        /// services used in this session.
        /// </summary>
        public void Dispose()
        {
            // Dispose all necessary services
            if (this.AnalysisService != null)
            {
                this.AnalysisService.Dispose();
                this.AnalysisService = null;
            }

            // Dispose all runspaces
            if (this.PowerShellSession != null)
            {
                this.PowerShellSession.Dispose();
                this.PowerShellSession = null;
            }
        }

        #endregion
    }
}
