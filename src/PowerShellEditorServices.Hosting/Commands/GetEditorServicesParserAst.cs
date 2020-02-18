//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.EditorServices.Commands
{
    
    /// <summary>
    /// The Get-EditorServicesParserAst command will parse and expand out data parsed by ast.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "EditorServicesParserAst")]
    public sealed class GetEditorServicesParserAst : PSCmdlet
    {

        /// <summary>
        /// The Scriptblock or block of code that gets parsed by ast.
        /// </summary>
        [Parameter(Mandatory = true)]
        public string ScriptBlock { get; set; }

        /// <summary>
        /// Specify a specific command type
        /// [System.Management.Automation.CommandTypes]
        /// </summary>
        [Parameter(Mandatory = true)]        
        public CommandTypes CommandType { get; set; }

        /// <summary>
        /// Specify a specific token type
        /// [System.Management.Automation.PSTokenType]
        /// </summary>
        [Parameter(Mandatory = true)]
        public PSTokenType PSTokenType { get; set; }

        protected override void EndProcessing()
        {
            var errors = new Collection<PSParseError>();

            var tokens =
                System.Management.Automation.PSParser.Tokenize(ScriptBlock, out errors)
                .Where(token => token.Type == this.PSTokenType)
                .OrderBy(token => token.Content);

            foreach (PSToken token in tokens)
            {
                if (PSTokenType == PSTokenType.Command)
                {
                    var result = SessionState.InvokeCommand.GetCommand(token.Content, CommandType);
                    WriteObject(result);
                }
                else {
                    WriteObject(token);
                }

            }
        }
    }
}