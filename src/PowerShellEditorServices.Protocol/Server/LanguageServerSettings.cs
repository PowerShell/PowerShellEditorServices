//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class LanguageServerSettings
    {
        public bool EnableProfileLoading { get; set; }

        public ScriptAnalysisSettings ScriptAnalysis { get; set; }

        public CodeFormattingSettings CodeFormatting { get; set; }

        public LanguageServerSettings()
        {
            this.ScriptAnalysis = new ScriptAnalysisSettings();
            this.CodeFormatting = new CodeFormattingSettings();
        }

        public void Update(
            LanguageServerSettings settings,
            string workspaceRootPath,
            ILogger logger)
        {
            if (settings != null)
            {
                this.EnableProfileLoading = settings.EnableProfileLoading;
                this.ScriptAnalysis.Update(
                    settings.ScriptAnalysis,
                    workspaceRootPath,
                    logger);
                this.CodeFormatting = new CodeFormattingSettings(settings.CodeFormatting);
            }
        }
    }

    public class ScriptAnalysisSettings
    {
        public bool? Enable { get; set; }

        public string SettingsPath { get; set; }

        public ScriptAnalysisSettings()
        {
            this.Enable = true;
        }

        public void Update(
            ScriptAnalysisSettings settings,
            string workspaceRootPath,
            ILogger logger)
        {
            if (settings != null)
            {
                this.Enable = settings.Enable;

                string settingsPath = settings.SettingsPath;

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
                        logger.Write(
                            LogLevel.Error,
                            "Could not resolve Script Analyzer settings path due to null or empty workspaceRootPath.");
                    }
                    else
                    {
                        settingsPath = Path.GetFullPath(Path.Combine(workspaceRootPath, settingsPath));
                    }
                }

                this.SettingsPath = settingsPath;
            }
        }
    }

    /// <summary>
    /// code formatting presets
    /// </summary>
    public enum CodeFormattingPreset
    {

        /// <summary>
        /// Use the formatting settings as-is.
        /// </summary>
        Custom,

        /// <summary>
        /// Configure the formatting settings to resemble the one true brace style variant of KR indent/brace style.
        /// </summary>
        OTBS,

        /// <summary>
        /// Configure the formatting settings to resemble the Allman indent/brace style.
        /// </summary>
        Allman
    }

    public class CodeFormattingSettings
    {
        /// <summary>
        /// Default constructor.
        /// </summary>>
        public CodeFormattingSettings()
        {

        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="codeFormattingSettings">An instance of type CodeFormattingSettings.</param>
        public CodeFormattingSettings(CodeFormattingSettings codeFormattingSettings)
        {
            if (codeFormattingSettings == null)
            {
                throw new ArgumentNullException(nameof(codeFormattingSettings));
            }

            foreach (var prop in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                prop.SetValue(this, prop.GetValue(codeFormattingSettings));
            }
        }

        public CodeFormattingPreset Preset { get; set; }
        public bool OpenBraceOnSameLine { get; set; }
        public bool NewLineAfterOpenBrace { get; set; }
        public bool NewLineAfterCloseBrace { get; set; }
        public bool WhitespaceBeforeOpenBrace { get; set; }
        public bool WhitespaceBeforeOpenParen { get; set; }
        public bool WhitespaceAroundOperator { get; set; }
        public bool WhitespaceAfterSeparator { get; set; }
        public bool IgnoreOneLineBlock { get; set; }
        public bool AlignPropertyValuePairs { get; set; }


        /// <summary>
        /// Get the settings hashtable that will be consumed by PSScriptAnalyzer.
        /// </summary>
        /// <param name="tabSize">The tab size in the number spaces.</param>
        /// <param name="insertSpaces">If true, insert spaces otherwise insert tabs for indentation.</param>
        /// <returns></returns>
        public Hashtable GetPSSASettingsHashtable(
            int tabSize,
            bool insertSpaces)
        {
            var settings = GetCustomPSSASettingsHashtable(tabSize, insertSpaces);
            var ruleSettings = (Hashtable)(settings["Rules"]);
            var closeBraceSettings = (Hashtable)ruleSettings["PSPlaceCloseBrace"];
            var openBraceSettings = (Hashtable)ruleSettings["PSPlaceOpenBrace"];
            switch(Preset)
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

                default:
                    break;
            }

            return settings;
        }

        private Hashtable GetCustomPSSASettingsHashtable(int tabSize, bool insertSpaces)
        {
            return new Hashtable
            {
                {"IncludeRules", new string[] {
                     "PSPlaceCloseBrace",
                     "PSPlaceOpenBrace",
                     "PSUseConsistentWhitespace",
                     "PSUseConsistentIndentation",
                     "PSAlignAssignmentStatement"
                }},
                {"Rules", new Hashtable {
                    {"PSPlaceOpenBrace", new Hashtable {
                        {"Enable", true},
                        {"OnSameLine", OpenBraceOnSameLine},
                        {"NewLineAfter", NewLineAfterOpenBrace},
                        {"IgnoreOneLineBlock", IgnoreOneLineBlock}
                    }},
                    {"PSPlaceCloseBrace", new Hashtable {
                        {"Enable", true},
                        {"NewLineAfter", NewLineAfterCloseBrace},
                        {"IgnoreOneLineBlock", IgnoreOneLineBlock}
                    }},
                    {"PSUseConsistentIndentation", new Hashtable {
                        {"Enable", true},
                        {"IndentationSize", tabSize}
                    }},
                    {"PSUseConsistentWhitespace", new Hashtable {
                        {"Enable", true},
                        {"CheckOpenBrace", WhitespaceBeforeOpenBrace},
                        {"CheckOpenParen", WhitespaceBeforeOpenParen},
                        {"CheckOperator", WhitespaceAroundOperator},
                        {"CheckSeparator", WhitespaceAfterSeparator}
                    }},
                    {"PSAlignAssignmentStatement", new Hashtable {
                        {"Enable", true},
                        {"CheckHashtable", AlignPropertyValuePairs}
                    }},
                }}
            };
        }
    }

    public class LanguageServerSettingsWrapper
        {
            // NOTE: This property is capitalized as 'Powershell' because the
            // mode name sent from the client is written as 'powershell' and
            // JSON.net is using camelCasing.

            public LanguageServerSettings Powershell { get; set; }
        }
    }
