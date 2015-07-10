//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// A class for containing the commandName, the command's
    /// possible signatures, and the script extent of the command
    /// </summary>
    public class ParameterSetSignatures
    {
        #region Properties
        /// <summary>
        /// Gets the name of the command
        /// </summary>
        public string CommandName { get; internal set; }

        /// <summary>
        /// Gets the collection of signatures for the command
        /// </summary>
        public ParameterSetSignature[] Signatures { get; internal set; }

        /// <summary>
        /// Gets the script extent of the command
        /// </summary>
        public ScriptRegion ScriptRegion { get; internal set; }
        #endregion
        
        /// <summary>
        /// Constructs an instance of a ParameterSetSignatures object
        /// </summary>
        /// <param name="commandInfoSet">Collection of parameter set info</param>
        /// <param name="foundSymbol"> The SymbolReference of the command</param>
        public ParameterSetSignatures(List<CommandParameterSetInfo> commandInfoSet, SymbolReference foundSymbol)
        {
            List<ParameterSetSignature> paramSetSignatures = new List<ParameterSetSignature>();
            foreach (CommandParameterSetInfo setInfo in commandInfoSet)
            {
                paramSetSignatures.Add(new ParameterSetSignature(setInfo));
            }
            Signatures = paramSetSignatures.ToArray();
            CommandName = foundSymbol.ScriptRegion.Text;
            ScriptRegion = foundSymbol.ScriptRegion;
        }
    }

    /// <summary>
    /// A class for containing the signature text and the collection of parameters for a signature
    /// </summary>
    public class ParameterSetSignature
    {
        #region Properties
        /// <summary>
        /// Gets the signature text
        /// </summary>
        public string SignatureText { get; internal set; }

        /// <summary>
        /// Gets the collection of parameters for the signature
        /// </summary>
        public IEnumerable<ParameterInfo> Parameters { get; internal set; }
        #endregion

        /// <summary>
        /// Constructs an instance of a ParameterSetSignature
        /// </summary>
        /// <param name="commandParamInfoSet">Collection of parameter info</param>
        public ParameterSetSignature(CommandParameterSetInfo commandParamInfoSet)
        {
            List<ParameterInfo> parameterInfo = new List<ParameterInfo>();
            foreach (CommandParameterInfo commandParameterInfo in commandParamInfoSet.Parameters)
            {
                parameterInfo.Add(new ParameterInfo(commandParameterInfo));
            }
            SignatureText = commandParamInfoSet.ToString();
            Parameters = parameterInfo.ToArray();
        }
    }

    /// <summary>
    /// A class for containing the parameter info of a parameter
    /// </summary>
    public class ParameterInfo
    {
        #region Properties
        /// <summary>
        /// Gets the name of the parameter
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the type of the parameter
        /// </summary>
        public string ParameterType { get; internal set; }

        /// <summary>
        /// Gets the position of the parameter
        /// </summary>
        public int Position { get; internal set; }
        
        /// <summary>
        /// Gets a boolean for whetheer or not the parameter is required
        /// </summary>
        public bool IsMandatory { get; internal set; }

        /// <summary>
        /// Gets the help message of the parameter
        /// </summary>
        public string HelpMessage { get; internal set; }
        #endregion

        /// <summary>
        /// Constructs an instance of a ParameterInfo object
        /// </summary>
        /// <param name="parameterInfo">Parameter info of the parameter</param>
        public ParameterInfo(CommandParameterInfo parameterInfo)
        {
            this.Name = parameterInfo.Name;
            this.ParameterType = parameterInfo.ParameterType.FullName;
            this.Position = parameterInfo.Position;
            this.IsMandatory = parameterInfo.IsMandatory;
            this.HelpMessage = parameterInfo.HelpMessage;
        }
    }
}
