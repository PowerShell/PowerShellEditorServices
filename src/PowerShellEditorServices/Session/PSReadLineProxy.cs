using System;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Session
{
    internal class PSReadLineProxy
    {
        private const string AddToHistoryMethodName = "AddToHistory";

        private const string SetKeyHandlerMethodName = "SetKeyHandler";

        private const string ReadKeyOverrideFieldName = "_readKeyOverride";

        private const string VirtualTerminalTypeName = "Microsoft.PowerShell.Internal.VirtualTerminal";

        private const string ForcePSEventHandlingMethodName = "ForcePSEventHandling";

        private static readonly Type[] s_setKeyHandlerTypes = new Type[4]
        {
            typeof(string[]),
            typeof(Action<ConsoleKeyInfo?, object>),
            typeof(string),
            typeof(string)
        };

        private static readonly Type[] s_addToHistoryTypes = new Type[1] { typeof(string) };

        private readonly FieldInfo _readKeyOverrideField;

        internal PSReadLineProxy(Type psConsoleReadLine)
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

            if (_readKeyOverrideField == null ||
                SetKeyHandler == null ||
                AddToHistory == null ||
                ForcePSEventHandling == null)
            {
                throw new InvalidOperationException();
            }
        }

        internal Action<string> AddToHistory { get; }

        internal Action<string[], Action<Nullable<ConsoleKeyInfo>, object>, string, string> SetKeyHandler { get; }

        internal Action ForcePSEventHandling { get; }

        internal void OverrideReadKey(Func<bool, ConsoleKeyInfo> readKeyFunc)
        {
            _readKeyOverrideField.SetValue(null, readKeyFunc);
        }
    }
}
