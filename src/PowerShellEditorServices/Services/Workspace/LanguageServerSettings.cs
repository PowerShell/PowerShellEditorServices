// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Newtonsoft.Json;

namespace Microsoft.PowerShell.EditorServices.Services.Configuration
{
    internal class LanguageServerSettings
    {
        private readonly object updateLock = new();
        public bool EnableProfileLoading { get; set; }
        public ScriptAnalysisSettings ScriptAnalysis { get; set; }
        public CodeFormattingSettings CodeFormatting { get; set; }
        public CodeFoldingSettings CodeFolding { get; set; }
        public PesterSettings Pester { get; set; }
        public string Cwd { get; set; }

        public LanguageServerSettings()
        {
            ScriptAnalysis = new ScriptAnalysisSettings();
            CodeFormatting = new CodeFormattingSettings();
            CodeFolding = new CodeFoldingSettings();
            Pester = new PesterSettings();
        }

        public void Update(
            LanguageServerSettings settings,
            string workspaceRootPath,
            ILogger logger)
        {
            if (settings is not null)
            {
                lock (updateLock)
                {
                    EnableProfileLoading = settings.EnableProfileLoading;
                    ScriptAnalysis.Update(settings.ScriptAnalysis, workspaceRootPath, logger);
                    CodeFormatting = new CodeFormattingSettings(settings.CodeFormatting);
                    CodeFolding.Update(settings.CodeFolding, logger);
                    Pester.Update(settings.Pester, logger);
                    Cwd = settings.Cwd;
                }
            }
        }
    }

    internal class ScriptAnalysisSettings
    {
        private readonly object updateLock = new();
        public bool Enable { get; set; }
        public string SettingsPath { get; set; }
        public ScriptAnalysisSettings() => Enable = true;

        public void Update(
            ScriptAnalysisSettings settings,
            string workspaceRootPath,
            ILogger logger)
        {
            if (settings is not null)
            {
                lock (updateLock)
                {
                    Enable = settings.Enable;
                    string settingsPath = settings.SettingsPath;

                    try
                    {
                        if (string.IsNullOrWhiteSpace(settingsPath))
                        {
                            settingsPath = null;
                        }
                        else if (!Path.IsPathRooted(settingsPath))
                        {
                            if (string.IsNullOrEmpty(workspaceRootPath))
                            {
                                // The workspace root path could be an empty string
                                // when the user has opened a PowerShell script file
                                // without opening an entire folder (workspace) first.
                                // In this case we should just log an error and let
                                // the specified settings path go through even though
                                // it will fail to load.
                                logger.LogError("Could not resolve Script Analyzer settings path due to null or empty workspaceRootPath.");
                            }
                            else
                            {
                                settingsPath = Path.GetFullPath(Path.Combine(workspaceRootPath, settingsPath));
                            }
                        }

                        SettingsPath = settingsPath;
                        logger.LogTrace($"Using Script Analyzer settings path - '{settingsPath ?? ""}'.");
                    }
                    catch (Exception ex) when (ex is NotSupportedException or PathTooLongException or SecurityException)
                    {
                        // Invalid chars in path like ${env:HOME} can cause Path.GetFullPath() to throw, catch such errors here
                        logger.LogException($"Invalid Script Analyzer settings path - '{settingsPath}'.", ex);
                        SettingsPath = null;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Code formatting presets.
    /// See https://en.wikipedia.org/wiki/Indent_style for details on indent and brace styles.
    /// </summary>
    internal enum CodeFormattingPreset
    {
        /// <summary>
        /// Use the formatting settings as-is.
        /// </summary>
        Custom,

        /// <summary>
        /// Configure the formatting settings to resemble the Allman indent/brace style.
        /// </summary>
        Allman,

        /// <summary>
        /// Configure the formatting settings to resemble the one true brace style variant of K&R indent/brace style.
        /// </summary>
        OTBS,

        /// <summary>
        /// Configure the formatting settings to resemble the Stroustrup brace style variant of K&amp;R indent/brace style.
        /// </summary>
        Stroustrup
    }

    /// <summary>
    /// Multi-line pipeline style settings.
    /// </summary>
    internal enum PipelineIndentationStyle
    {
        /// <summary>
        /// After the indentation level only once after the first pipeline and keep this level for the following pipelines.
        /// </summary>
        IncreaseIndentationForFirstPipeline,

        /// <summary>
        /// After every pipeline, keep increasing the indentation.
        /// </summary>
        IncreaseIndentationAfterEveryPipeline,

        /// <summary>
        /// Do not increase indentation level at all after pipeline.
        /// </summary>
        NoIndentation,

        /// <summary>
        /// Do not change pipeline indentation level at all.
        /// </summary>
        None,
    }

    internal class CodeFormattingSettings
    {
        /// <summary>
        /// Default constructor.
        /// </summary>>
        public CodeFormattingSettings() { }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="codeFormattingSettings">An instance of type CodeFormattingSettings.</param>
        public CodeFormattingSettings(CodeFormattingSettings codeFormattingSettings)
        {
            if (codeFormattingSettings is null)
            {
                throw new ArgumentNullException(nameof(codeFormattingSettings));
            }

            foreach (PropertyInfo prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                prop.SetValue(this, prop.GetValue(codeFormattingSettings));
            }
        }

        public bool AddWhitespaceAroundPipe { get; set; }
        public bool AutoCorrectAliases { get; set; }
        public bool UseConstantStrings { get; set; }
        public CodeFormattingPreset Preset { get; set; }
        public bool OpenBraceOnSameLine { get; set; }
        public bool NewLineAfterOpenBrace { get; set; }
        public bool NewLineAfterCloseBrace { get; set; }
        public PipelineIndentationStyle PipelineIndentationStyle { get; set; }
        public bool TrimWhitespaceAroundPipe { get; set; }
        public bool WhitespaceBeforeOpenBrace { get; set; }
        public bool WhitespaceBeforeOpenParen { get; set; }
        public bool WhitespaceAroundOperator { get; set; }
        public bool WhitespaceAfterSeparator { get; set; }
        public bool WhitespaceBetweenParameters { get; set; }
        public bool WhitespaceInsideBrace { get; set; }
        public bool IgnoreOneLineBlock { get; set; }
        public bool AlignPropertyValuePairs { get; set; }
        public bool UseCorrectCasing { get; set; }

        /// <summary>
        /// Get the settings hashtable that will be consumed by PSScriptAnalyzer.
        /// </summary>
        /// <param name="tabSize">The tab size in the number spaces.</param>
        /// <param name="insertSpaces">If true, insert spaces otherwise insert tabs for indentation.</param>
        /// <param name="logger">The logger instance.</param>
        public Hashtable GetPSSASettingsHashtable(
            int tabSize,
            bool insertSpaces,
            ILogger logger)
        {
            Hashtable settings = GetCustomPSSASettingsHashtable(tabSize, insertSpaces);
            Hashtable ruleSettings = settings["Rules"] as Hashtable;
            Hashtable closeBraceSettings = ruleSettings["PSPlaceCloseBrace"] as Hashtable;
            Hashtable openBraceSettings = ruleSettings["PSPlaceOpenBrace"] as Hashtable;

            switch (Preset)
            {
                case CodeFormattingPreset.Allman:
                    openBraceSettings["OnSameLine"] = false;
                    openBraceSettings["NewLineAfter"] = true;
                    closeBraceSettings["NewLineAfter"] = true;
                    break;

                case CodeFormattingPreset.OTBS:
                    openBraceSettings["OnSameLine"] = true;
                    openBraceSettings["NewLineAfter"] = true;
                    closeBraceSettings["NewLineAfter"] = false;
                    break;

                case CodeFormattingPreset.Stroustrup:
                    openBraceSettings["OnSameLine"] = true;
                    openBraceSettings["NewLineAfter"] = true;
                    closeBraceSettings["NewLineAfter"] = true;
                    break;
            }

            logger.LogDebug("Created formatting hashtable: {Settings}", JsonConvert.SerializeObject(settings));
            return settings;
        }

        private Hashtable GetCustomPSSASettingsHashtable(int tabSize, bool insertSpaces)
        {
            Hashtable ruleConfigurations = new()
            {
                {
                    "PSPlaceOpenBrace",
                    new Hashtable {
                    { "Enable", true },
                    { "OnSameLine", OpenBraceOnSameLine },
                    { "NewLineAfter", NewLineAfterOpenBrace },
                    { "IgnoreOneLineBlock", IgnoreOneLineBlock }
                }
                },
                {
                    "PSPlaceCloseBrace",
                    new Hashtable {
                    { "Enable", true },
                    { "NewLineAfter", NewLineAfterCloseBrace },
                    { "IgnoreOneLineBlock", IgnoreOneLineBlock }
                }
                },
                {
                    "PSUseConsistentIndentation",
                    new Hashtable {
                    { "Enable", true },
                    { "IndentationSize", tabSize },
                    { "PipelineIndentation", PipelineIndentationStyle },
                    { "Kind", insertSpaces ? "space" : "tab" }
                }
                },
                {
                    "PSUseConsistentWhitespace",
                    new Hashtable {
                    { "Enable", true },
                    { "CheckOpenBrace", WhitespaceBeforeOpenBrace },
                    { "CheckOpenParen", WhitespaceBeforeOpenParen },
                    { "CheckOperator", WhitespaceAroundOperator },
                    { "CheckSeparator", WhitespaceAfterSeparator },
                    { "CheckInnerBrace", WhitespaceInsideBrace },
                    { "CheckParameter", WhitespaceBetweenParameters },
                    { "CheckPipe", AddWhitespaceAroundPipe },
                    { "CheckPipeForRedundantWhitespace", TrimWhitespaceAroundPipe },
                }
                },
                {
                    "PSAlignAssignmentStatement",
                    new Hashtable {
                    { "Enable", true },
                    { "CheckHashtable", AlignPropertyValuePairs }
                }
                },
                {
                    "PSUseCorrectCasing",
                    new Hashtable {
                    { "Enable", UseCorrectCasing }
                }
                },
                {
                    "PSAvoidUsingDoubleQuotesForConstantString",
                    new Hashtable {
                    { "Enable", UseConstantStrings }
                }
                },
            };

            if (AutoCorrectAliases)
            {
                // Empty hashtable required to activate the rule,
                // since PSAvoidUsingCmdletAliases inherits from IScriptRule and not ConfigurableRule
                ruleConfigurations.Add("PSAvoidUsingCmdletAliases", new Hashtable());
            }

            return new Hashtable()
            {
                { "IncludeRules", new string[] {
                        "PSPlaceCloseBrace",
                        "PSPlaceOpenBrace",
                        "PSUseConsistentWhitespace",
                        "PSUseConsistentIndentation",
                        "PSAlignAssignmentStatement",
                        "PSAvoidUsingDoubleQuotesForConstantString",
                }},
                { "Rules", ruleConfigurations
                }
            };
        }
    }

    /// <summary>
    /// Code folding settings
    /// </summary>
    internal class CodeFoldingSettings
    {
        /// <summary>
        /// Whether the folding is enabled. Default is true as per VSCode
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Whether to show or hide the last line of a folding region. Default is true as per VSCode
        /// </summary>
        public bool ShowLastLine { get; set; } = true;

        /// <summary>
        /// Update these settings from another settings object
        /// </summary>
        public void Update(
            CodeFoldingSettings settings,
            ILogger logger)
        {
            if (settings is not null)
            {
                if (Enable != settings.Enable)
                {
                    Enable = settings.Enable;
                    logger.LogTrace(string.Format("Using Code Folding Enabled - {0}", Enable));
                }
                if (ShowLastLine != settings.ShowLastLine)
                {
                    ShowLastLine = settings.ShowLastLine;
                    logger.LogTrace(string.Format("Using Code Folding ShowLastLine - {0}", ShowLastLine));
                }
            }
        }
    }

    /// <summary>
    /// Pester settings
    /// </summary>
    public class PesterSettings
    {
        /// <summary>
        /// If specified, the lenses "run tests" and "debug tests" will appear above all Pester tests
        /// </summary>
        public bool CodeLens { get; set; } = true;

        /// <summary>
        /// Whether integration features specific to Pester v5 are enabled
        /// </summary>
        public bool UseLegacyCodeLens { get; set; }

        /// <summary>
        /// Update these settings from another settings object
        /// </summary>
        public void Update(
            PesterSettings settings,
            ILogger logger)
        {
            if (settings is null)
            {
                return;
            }

            if (CodeLens != settings.CodeLens)
            {
                CodeLens = settings.CodeLens;
                logger.LogTrace(string.Format("Using Pester Code Lens - {0}", CodeLens));
            }

            if (UseLegacyCodeLens != settings.UseLegacyCodeLens)
            {
                UseLegacyCodeLens = settings.UseLegacyCodeLens;
                logger.LogTrace(string.Format("Using Pester Legacy Code Lens - {0}", UseLegacyCodeLens));
            }
        }
    }

    /// <summary>
    /// Additional settings from the Language Client that affect Language Server operations but
    /// do not exist under the 'powershell' section
    /// </summary>
    internal class EditorFileSettings
    {
        /// <summary>
        /// Exclude files globs consists of hashtable with the key as the glob and a boolean value
        /// OR object with a predicate clause to indicate if the glob is in effect.
        /// </summary>
        public Dictionary<string, object> Exclude { get; set; }
    }

    /// <summary>
    /// Additional settings from the Language Client that affect Language Server operations but
    /// do not exist under the 'powershell' section
    /// </summary>
    internal class EditorSearchSettings
    {
        /// <summary>
        /// Exclude files globs consists of hashtable with the key as the glob and a boolean value
        /// OR object with a predicate clause to indicate if the glob is in effect.
        /// </summary>
        public Dictionary<string, object> Exclude { get; set; }

        /// <summary>
        /// Whether to follow symlinks when searching
        /// </summary>
        public bool FollowSymlinks { get; set; } = true;
    }

    internal class LanguageServerSettingsWrapper
    {
        // NOTE: This property is capitalized as 'Powershell' because the
        // mode name sent from the client is written as 'powershell' and
        // JSON.net is using camelCasing.
        public LanguageServerSettings Powershell { get; set; }

        // NOTE: This property is capitalized as 'Files' because the
        // mode name sent from the client is written as 'files' and
        // JSON.net is using camelCasing.
        public EditorFileSettings Files { get; set; }

        // NOTE: This property is capitalized as 'Search' because the
        // mode name sent from the client is written as 'search' and
        // JSON.net is using camelCasing.
        public EditorSearchSettings Search { get; set; }
    }
}
