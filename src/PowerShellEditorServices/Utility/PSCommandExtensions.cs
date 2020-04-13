//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class PSCommandExtensions
    {
        private static readonly Func<CommandInfo, Command> s_commandCtor;

        static PSCommandExtensions()
        {
            var ctor = typeof(Command).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                new[] { typeof(CommandInfo) },
                modifiers: null);

            ParameterExpression commandInfo = Expression.Parameter(typeof(CommandInfo), nameof(commandInfo));

            s_commandCtor = Expression.Lambda<Func<CommandInfo, Command>>(
                Expression.New(ctor, commandInfo),
                new[] { commandInfo })
                .Compile();
        }

        // PowerShell's missing an API for us to AddCommand using a CommandInfo.
        // An issue was filed here: https://github.com/PowerShell/PowerShell/issues/12295
        // This works around this by creating a `Command` and passing it into PSCommand.AddCommand(Command command)
        internal static PSCommand AddCommand(this PSCommand command, CommandInfo commandInfo)
        {
            var rsCommand = s_commandCtor(commandInfo);
            return command.AddCommand(rsCommand);
        }
    }
}
