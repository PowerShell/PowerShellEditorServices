//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class PSCommandExtensions
    {
        private static ConstructorInfo s_commandCtor =
            typeof(Command).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                new[] { typeof(CommandInfo) },
                modifiers: null);

        internal static PSCommand AddCommand(this PSCommand command, CommandInfo commandInfo)
        {
            var rsCommand = (Command) s_commandCtor
                .Invoke(new object[] { commandInfo });

            return command.AddCommand(rsCommand);
        }
    }
}
