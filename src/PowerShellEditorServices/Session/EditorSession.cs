﻿//
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

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        public void StartSession()
        {
            // Create a workspace to contain open files
            this.Workspace = new Workspace();

            // Initialize all services
            this.PowerShellContext = new PowerShellContext();
            this.LanguageService = new LanguageService(this.PowerShellContext);
            this.AnalysisService = new AnalysisService();
            this.DebugService = new DebugService(this.PowerShellContext);
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
