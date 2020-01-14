//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class PSReadLineProxy
    {
        private const string FieldMemberType = "field";

        private const string MethodMemberType = "method";

        private const string AddToHistoryMethodName = "AddToHistory";

        private const string SetKeyHandlerMethodName = "SetKeyHandler";

        private const string ReadKeyOverrideFieldName = "_readKeyOverride";

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

        private readonly FieldInfo _readKeyOverrideField;

        internal PSReadLineProxy(Type psConsoleReadLine, ILogger logger)
        {
            ForcePSEventHandling =
                (Action)psConsoleReadLine.GetMethod(
                    ForcePSEventHandlingMethodName,
                    BindingFlags.Static | BindingFlags.NonPublic)
                    ?.CreateDelegate(typeof(Action));

            AddToHistory = (Action<string>)psConsoleReadLine.GetMethod(
                AddToHistoryMethodName,
                s_addToHistoryTypes)
                ?.CreateDelegate(typeof(Action<string>));

            SetKeyHandler =
                (Action<string[], Action<ConsoleKeyInfo?, object>, string, string>)psConsoleReadLine.GetMethod(
                    SetKeyHandlerMethodName,
                    s_setKeyHandlerTypes)
                    ?.CreateDelegate(typeof(Action<string[], Action<ConsoleKeyInfo?, object>, string, string>));

            _readKeyOverrideField = psConsoleReadLine.GetTypeInfo().Assembly
                .GetType(VirtualTerminalTypeName)
                ?.GetField(ReadKeyOverrideFieldName, BindingFlags.Static | BindingFlags.NonPublic);

            if (_readKeyOverrideField == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    FieldMemberType,
                    ReadKeyOverrideFieldName,
                    logger);
            }

            if (SetKeyHandler == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    SetKeyHandlerMethodName,
                    logger);
            }

            if (AddToHistory == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    AddToHistoryMethodName,
                    logger);
            }

            if (ForcePSEventHandling == null)
            {
                throw NewInvalidPSReadLineVersionException(
                    MethodMemberType,
                    ForcePSEventHandlingMethodName,
                    logger);
            }
        }

        internal Action<string> AddToHistory { get; }

        internal Action<string[], Action<Nullable<ConsoleKeyInfo>, object>, string, string> SetKeyHandler { get; }

        internal Action ForcePSEventHandling { get; }

        internal void OverrideReadKey(Func<bool, ConsoleKeyInfo> readKeyFunc)
        {
            _readKeyOverrideField.SetValue(null, readKeyFunc);
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
