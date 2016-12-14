//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Templates
{
    /// <summary>
    /// Provides details about a file or project template.
    /// </summary>
    public class TemplateDetails
    {
        /// <summary>
        /// Gets or sets the title of the template.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the author of the template.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Gets or sets the version of the template.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the description of the template.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the template's comma-delimited string of tags.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        /// Gets or sets the template's folder path.
        /// </summary>
        public string TemplatePath { get; set; }
    }
}
