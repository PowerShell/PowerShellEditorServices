//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// A class for containing the commandName, the command's
    /// possible signatures, and the script extent of the command
    /// </summary>
    internal class ParameterSetSignatures
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
        public ParameterSetSignatures(IEnumerable<CommandParameterSetInfo> commandInfoSet, SymbolReference foundSymbol)
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
    internal class ParameterSetSignature
    {
        private static readonly ConcurrentDictionary<string, bool> commonParameterNames =
            new ConcurrentDictionary<string, bool>();

        static ParameterSetSignature()
        {
            commonParameterNames.TryAdd("Verbose", true);
            commonParameterNames.TryAdd("Debug", true);
            commonParameterNames.TryAdd("ErrorAction", true);
            commonParameterNames.TryAdd("WarningAction", true);
            commonParameterNames.TryAdd("InformationAction", true);
            commonParameterNames.TryAdd("ErrorVariable", true);
            commonParameterNames.TryAdd("WarningVariable", true);
            commonParameterNames.TryAdd("InformationVariable", true);
            commonParameterNames.TryAdd("OutVariable", true);
            commonParameterNames.TryAdd("OutBuffer", true);
            commonParameterNames.TryAdd("PipelineVariable", true);
        }

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
                if (!commonParameterNames.ContainsKey(commandParameterInfo.Name))
                {
                    parameterInfo.Add(new ParameterInfo(commandParameterInfo));
                }
            }

            SignatureText = commandParamInfoSet.ToString();
            Parameters = parameterInfo.ToArray();
        }
    }

    /// <summary>
    /// A class for containing the parameter info of a parameter
    /// </summary>
    internal class ParameterInfo
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
            this.Name = "-" + parameterInfo.Name;
            this.ParameterType = parameterInfo.ParameterType.FullName;
            this.Position = parameterInfo.Position;
            this.IsMandatory = parameterInfo.IsMandatory;
            this.HelpMessage = parameterInfo.HelpMessage;
        }
    }
}
