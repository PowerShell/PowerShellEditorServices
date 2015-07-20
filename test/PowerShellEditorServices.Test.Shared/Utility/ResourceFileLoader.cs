//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using System.IO;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Utility
{
    public class ResourceFileLoader
    {
        private Assembly resourceAssembly;

        public ResourceFileLoader(Assembly resourceAssembly = null)
        {
            if (resourceAssembly == null)
            {
                resourceAssembly = Assembly.GetExecutingAssembly();
            }

            this.resourceAssembly = resourceAssembly;
        }

        public ScriptFile LoadFile(string fileName)
        {
            // Convert the filename to the proper format
            string resourceName =
                string.Format(
                    "{0}.{1}",
                    resourceAssembly.GetName().Name,
                    fileName.Replace('\\', '.'));

            using (Stream stream = resourceAssembly.GetManifestResourceStream(resourceName))
            using (StreamReader streamReader = new StreamReader(stream))
            {
                return new ScriptFile(fileName, streamReader);
            }
        }
    }
}
