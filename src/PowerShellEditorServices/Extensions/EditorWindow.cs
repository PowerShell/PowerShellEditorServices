//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a PowerShell-facing API which allows scripts to
    /// interact with the editor's window.
    /// </summary>
    public class EditorWindow
    {
        #region Private Fields

        private IEditorOperations editorOperations;

        #endregion

        #region Constructors

        internal EditorWindow(IEditorOperations editorOperations)
        {
            this.editorOperations = editorOperations;
        }

        #endregion 

        #region Public Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void ShowInformationMessage(string message)
        {
            this.editorOperations.ShowInformationMessage(message).Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void ShowErrorMessage(string message)
        {
            this.editorOperations.ShowErrorMessage(message).Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void ShowWarningMessage(string message)
        {
            this.editorOperations.ShowWarningMessage(message).Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void SetStatusBarMessage(string message)
        {
            this.editorOperations.SetStatusBarMessage(message).Wait();
        }

        #endregion 
    }
}
