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
                (Action)GetMethod(
                    psConsoleReadLine,
                    ForcePSEventHandlingMethodName,
                    Type.EmptyTypes,
                    BindingFlags.Static | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action));

            AddToHistory =
                (Action<string>)GetMethod(
                    psConsoleReadLine,
                    AddToHistoryMethodName,
                    s_addToHistoryTypes)
                .CreateDelegate(typeof(Action<string>));

            SetKeyHandler =
                (Action<string[], Action<ConsoleKeyInfo?, object>, string, string>)GetMethod(
                    psConsoleReadLine,
                    SetKeyHandlerMethodName,
                    s_setKeyHandlerTypes)
                .CreateDelegate(typeof(Action<string[], Action<ConsoleKeyInfo?, object>, string, string>));

            _readKeyOverrideField = psConsoleReadLine.GetTypeInfo().Assembly
                .GetType(VirtualTerminalTypeName)
                ?.GetField(ReadKeyOverrideFieldName, BindingFlags.Static | BindingFlags.NonPublic);

            if (_readKeyOverrideField == null)
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

        private static MethodInfo GetMethod(
            Type psConsoleReadLine,
            string name,
            Type[] types,
            BindingFlags flags = BindingFlags.Public | BindingFlags.Static)
        {
            // Shouldn't need this compiler directive after switching to netstandard2.0
            #if CoreCLR
            var method = psConsoleReadLine.GetMethod(
                name,
                flags);
            #else
            var method = psConsoleReadLine.GetMethod(
                name,
                flags,
                null,
                types,
                types.Length == 0 ? new ParameterModifier[0] : new[] { new ParameterModifier(types.Length) });
            #endif

            if (method == null)
            {
                throw new InvalidOperationException();
            }

            return method;
        }
    }
}
