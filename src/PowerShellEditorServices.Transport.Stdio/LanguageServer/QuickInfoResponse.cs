//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("quickinfo")]
    public class QuickInfoResponse : ResponseBase<QuickInfoResponseBody>
    {
    }

    public class QuickInfoResponseBody
    {
        /// <summary>
        /// Gets or sets the symbol's kind, such as "className",
        /// "parameterName", or "text".
        /// </summary>
        public string Kind { get; set; }
    
        /// <summary>
        /// Gets or sets optional modifiers for the symbol such as
        /// "public".
        /// </summary>
        public string KindModifiers { get; set; }
    
        /// <summary>
        /// Gets or sets the start location of the symbol.
        /// </summary>
        public Location Start { get; set; }
    
        /// <summary>
        /// Gets or sets the end location of the symbol, one
        /// character past the end of the symbol.
        /// </summary>
        public Location End { get; set; }
    
        /// <summary>
        /// Gets or sets the display string of the symbol, typically containing
        /// the type and kind in addition to the name.
        /// </summary>
        public string DisplayString { get; set; }
    
        /// <summary>
        /// Gets or sets the documentation associated with the symbol.
        /// </summary>
        public string Documentation { get; set; }
    }
}

