//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Model
{
    public class Source
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public int? SourceReference { get; set; }
    }
}

