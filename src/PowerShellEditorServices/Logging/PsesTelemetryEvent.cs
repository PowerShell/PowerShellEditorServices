//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Logging
{
    // This inheirits from Dictionary so that it can be passed in to SendTelemetryEvent()
    // which takes in an IDictionary<string, object>
    // However, I wanted creation to be easy so you can do
    // new PsesTelemetryEvent { EventName = "eventName", Data = data }
    internal class PsesTelemetryEvent : Dictionary<string, object>
    {
        public string EventName
        {
            get
            {
                return this["EventName"].ToString() ?? "PsesEvent";
            }
            set
            {
                this["EventName"] = value;
            }
        }

        public JObject Data
        {
            get
            {
                return this["Data"] as JObject ?? new JObject();
            }
            set
            {
                this["Data"] = value;
            }
        }
    }
}
