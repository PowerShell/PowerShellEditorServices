//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

using Internal = Microsoft.PowerShell.EditorServices.Services.Symbols;

// TODO: This is currently disabled in the csproj
//       Redesign this API and bring it back once it's fit for purpose

namespace Microsoft.PowerShell.EditorServices.Extensions.Services
{
    /// <summary>
    /// A way to define symbols on a higher level
    /// </summary>
    public enum SymbolType
    {
        /// <summary>
        /// The symbol type is unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The symbol is a vairable
        /// </summary>
        Variable = 1,

        /// <summary>
        /// The symbol is a function
        /// </summary>
        Function = 2,

        /// <summary>
        /// The symbol is a parameter
        /// </summary>
        Parameter = 3,

        /// <summary>
        /// The symbol is a DSC configuration
        /// </summary>
        Configuration = 4,

        /// <summary>
        /// The symbol is a workflow
        /// </summary>
        Workflow = 5,

        /// <summary>
        /// The symbol is a hashtable key
        /// </summary>
        HashtableKey = 6,
    }

    /// <summary>
    /// Interface to instantiate to create a provider of document symbols.
    /// </summary>
    public interface IDocumentSymbolProvider
    {
        /// <summary>
        /// The unique ID of this provider.
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Run this provider to provide symbols to PSES from the given file.
        /// </summary>
        /// <param name="scriptFile">The script file to provide symbols for.</param>
        /// <returns>Symbols about the file.</returns>
        IEnumerable<SymbolReference> ProvideDocumentSymbols(IEditorScriptFile scriptFile);
    }

    /// <summary>
    /// A class that holds the type, name, script extent, and source line of a symbol
    /// </summary>
    [DebuggerDisplay("SymbolType = {SymbolType}, SymbolName = {SymbolName}")]
    public class SymbolReference
    {
        /// <summary>
        /// Constructs an instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="filePath">The file path of the symbol</param>
        /// <param name="sourceLine">The line contents of the given symbol (defaults to empty string)</param>
        public SymbolReference(SymbolType symbolType, IScriptExtent scriptExtent)
            : this(symbolType, scriptExtent.Text, scriptExtent)
        {
        }

        /// <summary>
        /// Constructs and instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="symbolName">The name of the symbol</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="filePath">The file path of the symbol</param>
        /// <param name="sourceLine">The line contents of the given symbol (defaults to empty string)</param>
        public SymbolReference(
            SymbolType symbolType,
            string symbolName,
            IScriptExtent scriptExtent)
            : this(symbolType, scriptExtent, symbolName, filePath: string.Empty, sourceLine: string.Empty)
        {
        }

        public SymbolReference(
            SymbolType symbolType,
            IScriptExtent scriptExtent,
            string symbolName,
            string filePath)
            : this(symbolType, scriptExtent, symbolName, filePath, sourceLine: string.Empty)
        {
        }

        public SymbolReference(SymbolType symbolType, IScriptExtent scriptExtent, string symbolName, string filePath, string sourceLine)
        {
            // TODO: Verify params
            SymbolType = symbolType;
            ScriptRegion = ScriptRegion.Create(scriptExtent);
            SymbolName = symbolName;
            FilePath = filePath;
            SourceLine = sourceLine;

            // TODO: Make sure end column number usage is correct
        }

        #region Properties

        /// <summary>
        /// Gets the symbol's type
        /// </summary>
        public SymbolType SymbolType { get; }

        /// <summary>
        /// Gets the name of the symbol
        /// </summary>
        public string SymbolName { get; }

        /// <summary>
        /// Gets the script extent of the symbol
        /// </summary>
        public ScriptRegion ScriptRegion { get; }

        /// <summary>
        /// Gets the contents of the line the given symbol is on
        /// </summary>
        public string SourceLine { get; }

        /// <summary>
        /// Gets the path of the file in which the symbol was found.
        /// </summary>
        public string FilePath { get; internal set; }

        #endregion
    }

    /// <summary>
    /// Service for registration of document symbol providers in PSES.
    /// </summary>
    public interface IDocumentSymbolService
    {
        /// <summary>
        /// Register a document symbol provider by its ID.
        /// If another provider is already registered by the same ID, this will fail and return false.
        /// </summary>
        /// <param name="documentSymbolProvider">The document symbol provider to register.</param>
        /// <returns>True if the symbol provider was successfully registered, false otherwise.</returns>
        bool RegisterDocumentSymbolProvider(IDocumentSymbolProvider documentSymbolProvider);

        /// <summary>
        /// Deregister a symbol provider of the given ID.
        /// </summary>
        /// <param name="providerId">The ID of the provider to deregister.</param>
        /// <returns>True if a provider by the given ID was deregistered, false if no such provider was found.</returns>
        bool DeregisterDocumentSymbolProvider(string providerId);
    }

    internal class DocumentSymbolService : IDocumentSymbolService
    {
        private readonly SymbolsService _symbolsService;

        internal DocumentSymbolService(SymbolsService symbolsService)
        {
            _symbolsService = symbolsService;
        }

        public bool RegisterDocumentSymbolProvider(IDocumentSymbolProvider documentSymbolProvider)
        {
            return _symbolsService.TryRegisterDocumentSymbolProvider(new ExternalDocumentSymbolProviderAdapter(documentSymbolProvider));
        }

        public bool DeregisterDocumentSymbolProvider(string providerId)
        {
            return _symbolsService.DeregisterCodeLensProvider(providerId);
        }
    }

    internal class ExternalDocumentSymbolProviderAdapter : Internal.IDocumentSymbolProvider
    {
        private readonly IDocumentSymbolProvider _symbolProvider;

        public ExternalDocumentSymbolProviderAdapter(
            IDocumentSymbolProvider externalDocumentSymbolProvider)
        {
            _symbolProvider = externalDocumentSymbolProvider;
        }

        public string ProviderId => _symbolProvider.ProviderId;

        public IEnumerable<Internal.ISymbolReference> ProvideDocumentSymbols(ScriptFile scriptFile)
        {
            foreach (SymbolReference symbolReference in _symbolProvider.ProvideDocumentSymbols(new EditorScriptFile(scriptFile)))
            {
                yield return new ExternalSymbolReferenceAdapter(symbolReference);
            }
        }
    }

    internal class ExternalSymbolReferenceAdapter : Internal.ISymbolReference
    {
        private readonly SymbolReference _symbolReference;

        public ExternalSymbolReferenceAdapter(SymbolReference symbolReference)
        {
            _symbolReference = symbolReference;
        }

        public Internal.SymbolType SymbolType => _symbolReference.SymbolType.ToInternalSymbolType();

        public string SymbolName => _symbolReference.SymbolName;

        public ScriptRegion ScriptRegion => _symbolReference.ScriptRegion;

        public string SourceLine => _symbolReference.SourceLine;

        public string FilePath => _symbolReference.FilePath;
    }

    internal static class SymbolTypeExtensions
    {
        public static Internal.SymbolType ToInternalSymbolType(this SymbolType symbolType)
        {
            switch (symbolType)
            {
                case SymbolType.Unknown:
                    return Internal.SymbolType.Unknown;

                case SymbolType.Variable:
                    return Internal.SymbolType.Variable;

                case SymbolType.Function:
                    return Internal.SymbolType.Function;

                case SymbolType.Parameter:
                    return Internal.SymbolType.Parameter;

                case SymbolType.Configuration:
                    return Internal.SymbolType.Configuration;

                case SymbolType.Workflow:
                    return Internal.SymbolType.Workflow;

                case SymbolType.HashtableKey:
                    return Internal.SymbolType.HashtableKey;

                default:
                    throw new InvalidOperationException($"Unknown symbol type '{symbolType}'");
            }
        }
    }
}

