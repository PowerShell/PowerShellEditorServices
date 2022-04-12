// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Logging
{
    // This inherits from Dictionary so that it can be passed in to SendTelemetryEvent()
    // which takes in an IDictionary<string, object>
    // However, I wanted creation to be easy so you can do
    // new PsesTelemetryEvent { EventName = "eventName", Data = data }
    internal class PsesTelemetryEvent : Dictionary<string, object>
    {
        public string EventName
        {
            get => this["EventName"].ToString() ?? "PsesEvent";
            set => this["EventName"] = value;
        }

        public JObject Data
        {
            get => this["Data"] as JObject ?? new JObject();
            set => this["Data"] = value;
        }
    }
}
