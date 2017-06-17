//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Components;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.VSCode
{
    public static class ComponentRegistration
    {
        public static void Register(IComponentRegistry components)
        {
            ILogger logger = components.Get<ILogger>();
            logger.Write(LogLevel.Normal, "PowerShell Editor Services VS Code module loaded.");
        }
    }
}
