//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Defines an interface for prompt handler implementations.
    /// </summary>
    public interface IPromptHandler
    {
        /// <summary>
        /// Implements behavior to handle the user's response.
        /// </summary>
        /// <param name="responseString">The string representing the user's response.</param>
        /// <returns>
        /// True if the prompt is complete, false if the prompt is 
        /// still waiting for a valid response.
        /// </returns>
        bool HandleResponse(string responseString);

        /// <summary>
        /// Called when the active prompt should be cancelled.
        /// </summary>
        void CancelPrompt();
    }
}

