//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal class PSReadLineProxy
    {
        private const string FieldMemberType = "field";

        private const string MethodMemberType = "method";

        private const string AddToHistoryMethodName = "AddToHistory";

        private const string SetKeyHandlerMethodName = "SetKeyHandler";

        private const string ReadLineMethodName = "ReadLine";

        private const string ReadKeyOverrideFieldName = "_readKeyOverride";

        private const string HandleIdleOverrideName = "_handleIdleOverride";

        private const string VirtualTerminalTypeName = "Microsoft.PowerShell.Internal.VirtualTerminal";

        private const string ForcePSEventHandlingMethodName = "ForcePSEventHandling";

        private static readonly Type[] s_setKeyHandlerTypes =
        {
            typeof(string[]),
            typeof(Action<ConsoleKeyInfo?, object>),
            typeof(string),
            typeof(string)
        };

        private static readonly Type[] s_addToHistoryTypes = { typeof(string) };

        private static readonly string _psReadLineModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(typeof(PSReadLineProxy).Assembly.Location),
                "..",
                "..",
                "..",
                "PSReadLine"));

        private static readonly string ReadLineInitScript = $@"
            [System.Diagnostics.DebuggerHidden()]
            [System.Diagnostics.DebuggerStepThrough()]
            param()
            end {{
                $module = Get-Module -ListAvailable PSReadLine |
                    Where-Object {{ $_.Version -ge '2.0.2' }} |
                    Sort-Object -Descending Version |
                    Select-Object -First 1
                if (-not $module) {{
                    Import-Module '{_psReadLineModulePath.Replace("'", "''")}'
                    return [Microsoft.PowerShell.PSConsoleReadLine]
                }}

                Import-Module -ModuleInfo $module
                return [Microsoft.PowerShell.PSConsoleReadLine]
            }}";

        public static PSReadLineProxy LoadAndCreate(
            ILoggerFactory loggerFactory,
            SMA.PowerShell pwsh)
        {
            Type psConsoleReadLineType = pwsh.AddScript(ReadLineInitScript).InvokeAndClear<Type>().FirstOrDefault();

            Type type = Type.GetType("Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine2");

            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            return new PSReadLineProxy(loggerFactory, psConsoleReadLineType);
        }

        private readonly FieldInfo _readKeyOverrideField;

        private readonly FieldInfo _handleIdleOverrideField;

        private readonly ILogger _logger;

        public PSReadLineProxy(
            ILoggerFactory loggerFactory,
            Type psConsoleReadLine)
        {
            _logger = loggerFactory.CreateLogger<PSReadLineProxy>();

            ReadLine = (Func<Runspace, EngineIntrinsics, CancellationToken, string>)psConsoleReadLine.GetMethod(
                ReadLineMethodName,
                new[] { typeof(Runspace), typeof(EngineIntrinsics), typeof(CancellationToken) })
                ?.CreateDelegate(typeof(Func<Runspace, EngineIntrinsics, CancellationToken, string>));

            ForcePSEventHandling = (Action)psConsoleReadLine.GetMethod(
                ForcePSEventHandlingMethodName,
                BindingFlags.Static | BindingFlags.NonPublic)
                ?.CreateDelegate(typeof(Action));

            AddToHistory = (Action<string>)psConsoleReadLine.GetMethod(
                AddToHistoryMethodName,
                s_addToHistoryTypes)
                ?.CreateDelegate(typeof(Action<string>));

            SetKeyHandler = (Action<string[], Action<ConsoleKeyInfo?, object>, string, string>)psConsoleReadLine.GetMethod(
                SetKeyHandlerMethodName,
                s_setKeyHandlerTypes)
                ?.CreateDelegate(typeof(Action<string[], Action<ConsoleKeyInfo?, object>, string, string>));

            _handleIdleOverrideField = psConsoleReadLine.GetField(HandleIdleOverrideName, BindingFlags.Static | BindingFlags.NonPublic);

            _readKeyOverrideField = psConsoleReadLine.GetTypeInfo().Assembly
                .GetType(VirtualTerminalTypeName)
                ?.GetField(ReadKeyOverrideFieldName, BindingFlags.Static | BindingFlags.NonPublic);

            if (_readKeyOverrideField == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    FieldMemberType,
                    ReadKeyOverrideFieldName,
                    _logger);
            }

            if (ReadLine == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    ReadLineMethodName,
                    _logger);
            }

            if (SetKeyHandler == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    SetKeyHandlerMethodName,
                    _logger);
            }

            if (AddToHistory == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    AddToHistoryMethodName,
                    _logger);
            }

            if (ForcePSEventHandling == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    ForcePSEventHandlingMethodName,
                    _logger);
            }
        }

        internal Action<string> AddToHistory { get; }

        internal Action<string[], Action<ConsoleKeyInfo?, object>, string, string> SetKeyHandler { get; }

        internal Action ForcePSEventHandling { get; }

        internal Func<Runspace, EngineIntrinsics, CancellationToken, string> ReadLine { get; }

        internal void OverrideReadKey(Func<bool, ConsoleKeyInfo> readKeyFunc)
        {
            _readKeyOverrideField.SetValue(null, readKeyFunc);
        }

        internal void OverrideIdleHandler(Action idleAction)
        {
            _handleIdleOverrideField.SetValue(null, idleAction);
        }

        private static InvalidOperationException NewInvalidPSReadLineVersionException(
            string memberType,
            string memberName,
            ILogger logger)
        {
            logger.LogError(
                $"The loaded version of PSReadLine is not supported. The {memberType} \"{memberName}\" was not found.");

            return new InvalidOperationException();
        }
    }
}
