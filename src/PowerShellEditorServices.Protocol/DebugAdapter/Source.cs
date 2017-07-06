//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class Source
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public int? SourceReference { get; set; }

        /// <summary>
        /// Gets an optional hint for how to present the source in the UI. A value of 'deemphasize' 
        /// can be used to indicate that the source is not available or that it is skipped on stepping.
        /// </summary>
        public string PresentationHint { get; set; }
    }

    /// <summary>
    /// An optional hint for how to present source in the UI. 
    /// </summary>
    public enum SourcePresentationHint
    {
        /// <summary>
        /// Dispays the source normally.
        /// </summary>
        Normal,

        /// <summary>
        /// Display the source emphasized.
        /// </summary>
        Emphasize,

        /// <summary>
        /// Display the source deemphasized.
        /// </summary>
        Deemphasize
    }
}

