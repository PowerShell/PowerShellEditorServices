//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    public class TextSpan
    {
        public Location Start { get; set; }
        public Location End { get; set; }
    }

    public class FileSpan : TextSpan
    {
        public string File { get; set; }
    }

    public class Location
    {
        public int Line { get; set; }

        public int Offset { get; set; }
    }

}
