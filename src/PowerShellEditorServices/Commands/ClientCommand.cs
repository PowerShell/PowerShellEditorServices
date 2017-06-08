//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Commands
{
    /// <summary>
    /// Provides details for a command which will be executed
    /// in the host editor.
    /// </summary>
    public class ClientCommand
    {
        /// <summary>
        /// Gets the identifying name of the command.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the display title of the command.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the array of objects which are passed as
        /// arguments to the command.
        /// </summary>
        public object[] Arguments { get; private set; }

        /// <summary>
        /// Creates an instance of the ClientCommand class.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="commandTitle">The display title of the command.</param>
        /// <param name="arguments">The arguments to be passed to the command.</param>
        public ClientCommand(
            string commandName,
            string commandTitle,
            object[] arguments)
        {
            this.Name = commandName;
            this.Title = commandTitle;
            this.Arguments = arguments;
        }
    }
}
