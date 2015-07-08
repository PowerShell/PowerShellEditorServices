//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Request;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    public class MessageParser
    {
        private JsonSerializer jsonSerializer = 
            JsonSerializer.Create(
                Constants.JsonSerializerSettings);

        // TODO: Load these dynamically
        private Dictionary<string, Type> requestTypes = new Dictionary<string, Type>
        {
            { "open", typeof(OpenFileRequest) },
            { "change", typeof(ChangeFileRequest) },
            { "geterr", typeof(ErrorRequest)},
            { "completions", typeof(CompletionsRequest) },
            //{ "completionEntryDetails", typeof(CompletionDetailsRequest) },
            //{ "occurrences", typeof(OccurrencesRequest) },
            //{ "references", typeof(ReferencesRequest) },
            //{ "definition", typeof(DeclarationRequest) },
            //{ "signatureHelp", typeof(SignatureHelpRequestArgs) },
            { "replExecute", typeof(ReplExecuteRequest) }
        };

        private Dictionary<string, Type> responseTypes = new Dictionary<string, Type>
        {
            { "completions", typeof(CompletionsResponse) },
            //{ "completionEntryDetails", typeof(CompletionDetailsResponse) },
            //{ "occurrences", typeof(OccurrencesResponse) },
            //{ "references", typeof(ReferencesResponse) },
            //{ "definition", typeof(DefinitionResponse) },
            //{ "signatureHelp", typeof(SignatureHelpResponse) },
            { "replPromptChoice", typeof(ReplPromptChoiceResponse) }
        };

        private Dictionary<string, Type> eventTypes = new Dictionary<string, Type>
        {
            { "started", typeof(StartedEvent)},
            { "syntaxDiag", typeof(DiagnosticEvent)},
            { "semanticDiag", typeof(DiagnosticEvent)},
            { "replPromptChoice", typeof(ReplPromptChoiceEvent) },
            { "replWriteOutput", typeof(ReplWriteOutputEvent) }
        };

        public MessageBase ParseMessage(string messageJson)
        {
            // Parse the JSON string
            JObject messageObject = JObject.Parse(messageJson);

            // Determine the message type and deserialize it
            Type messageType = typeof(MessageBase);
            try
            {
                messageType = this.GetMessageType(messageObject);
            }
            catch (MessageParseException)
            {
                // TODO: Still need a better way to handle this
            }

            return (MessageBase)messageObject.ToObject(messageType, this.jsonSerializer);
        }

        private Type GetMessageType(JObject messageObject)
        {
            Type messageType = null;

            string messageTypeName = null;
            if (TryGetValueString(messageObject, "type", out messageTypeName))
            {
                switch (messageTypeName)
                {
                    case "request":
                        return this.GetRequestType(messageObject);

                    case "response":
                        return this.GetResponseType(messageObject);

                    case "event":
                        return this.GetEventType(messageObject);

                    default:
                        throw new MessageParseException(
                            messageObject.ToString(),
                            "Unknown message type: {0}",
                            messageTypeName);
                }
            }

            return messageType;
        }

        private Type GetRequestType(JObject messageObject)
        {
            string commandTypeName = null;
            if (TryGetValueString(messageObject, "command", out commandTypeName))
            {
                Type messageType;
                if (this.requestTypes.TryGetValue(commandTypeName, out messageType))
                {
                    return messageType;
                }
                else
                {
                    throw new MessageParseException(
                        "Unknown request command: {0}",
                        commandTypeName);
                }
            }
            else
            {
                throw new MessageParseException(
                    messageObject.ToString(),
                    "Request has no 'command' field.");
            }
        }

        private Type GetResponseType(JObject messageObject)
        {
            string commandTypeName = null;
            if (TryGetValueString(messageObject, "command", out commandTypeName))
            {
                // TODO: Is command name always expected to be there?  Fail if it's missing or null
                Type messageType;
                if (commandTypeName == null || 
                    !this.responseTypes.TryGetValue(commandTypeName, out messageType))
                {
                    messageType = typeof(ResponseBase<object>);
                }

                return messageType;
            }
            else
            {
                throw new MessageParseException(
                    messageObject.ToString(),
                    "Response has no 'command' field.");    
            }
        }

        private Type GetEventType(JObject messageObject)
        {
            string eventTypeName = null;
            if (TryGetValueString(messageObject, "event", out eventTypeName))
            {
                Type messageType;
                if (eventTypeName == null || 
                    !this.eventTypes.TryGetValue(eventTypeName, out messageType))
                {
                    messageType = typeof(EventBase<object>);
                }

                return messageType;
            }
            else
            {
                throw new MessageParseException(
                    messageObject.ToString(),
                    "Event has no 'event' field.");
            }
        }

        private static bool TryGetValueString(JObject jsonObject, string valueName, out string valueString)
        {
            valueString = null;

            JToken valueToken = null;
            if (jsonObject.TryGetValue(valueName, out valueToken))
            {
                JValue realValueToken = valueToken as JValue;
                if (realValueToken != null)
                {
                    if (realValueToken.Type == JTokenType.String)
                    {
                        valueString = (string)realValueToken.Value;
                    }
                    else if (realValueToken.Type == JTokenType.Null)
                    {
                        // If the value is null, return it too
                        valueString = null;
                    }
                    else
                    {
                        // No other value type is valid
                        return false;
                    }

                    return true;
                }
                else
                {
                    // TODO: Error?
                }
            }

            return false;
        }
    }
}
