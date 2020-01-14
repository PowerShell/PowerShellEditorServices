//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Implementation-free class designed to safely allow PowerShell Editor Services to be loaded in an obvious way.
    /// Referencing this class will force looking for and loading the PSES assembly if it's not already loaded.
    /// </summary>
    internal static class EditorServicesLoading
    {
        internal static void LoadEditorServicesForHost()
        {
            // No op that forces loading this assembly
        }
    }
}
