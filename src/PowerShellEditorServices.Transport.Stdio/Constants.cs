//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio
{
    public static class Constants
    {
        public const string ContentLengthString = "Content-Length: ";
        public static readonly JsonSerializerSettings JsonSerializerSettings;

        static Constants()
        {
            JsonSerializerSettings = new JsonSerializerSettings();

            // Camel case all object properties
            JsonSerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver();

            // Convert enum values to their string representation with camel casing
            JsonSerializerSettings.Converters.Add(
                new StringEnumConverter
                {
                    CamelCaseText = true
                });
        }
    }
}
