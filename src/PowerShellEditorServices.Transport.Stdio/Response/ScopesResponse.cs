//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("scopes")]
    public class ScopesResponse : ResponseBase<ScopesResponseBody>
    {
    }

    public class ScopesResponseBody
    {
        public Scope[] Scopes { get; set; }
    }
}

