//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Components;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.PowerShell.EditorServices.VSCode.CustomViews;

namespace Microsoft.PowerShell.EditorServices.VSCode
{
    /// <summary>
    /// Methods for registering components from this module into
    /// the editor session.
    /// </summary>
    public static class ComponentRegistration
    {
        /// <summary>
        /// Registers the feature components in this module with the
        /// host editor.
        /// </summary>
        /// <param name="components">
        /// The IComponentRegistry where feature components will be registered.
        /// </param>
        public static void Register(IComponentRegistry components)
        {
            IPsesLogger logger = components.Get<IPsesLogger>();

            components.Register<IHtmlContentViews>(
                new HtmlContentViewsFeature(
                    components.Get<IMessageSender>(),
                    logger));

            logger.Write(
                LogLevel.Normal,
                "PowerShell Editor Services VS Code module loaded.");
        }
    }
}
