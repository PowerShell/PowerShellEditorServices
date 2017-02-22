//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Defines an interface for requesting prompt handlers in
    /// a given user interface context.
    /// </summary>
    public interface IPromptHandlerContext
    {
        /// <summary>
        /// Creates a new ChoicePromptHandler instance so that
        /// the caller can display a choice prompt to the user.
        /// </summary>
        /// <returns>
        /// A new ChoicePromptHandler instance.
        /// </returns>
        ChoicePromptHandler GetChoicePromptHandler();

        /// <summary>
        /// Creates a new InputPromptHandler instance so that
        /// the caller can display an input prompt to the user.
        /// </summary>
        /// <returns>
        /// A new InputPromptHandler instance.
        /// </returns>
        InputPromptHandler GetInputPromptHandler();

        CredentialPromptHandler GetCredentialPromptHandler();
    }
}

