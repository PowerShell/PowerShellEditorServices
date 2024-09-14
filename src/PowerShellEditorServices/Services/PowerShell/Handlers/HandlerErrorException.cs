// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers;

/// <summary>
/// A convenience exception for handlers to throw when a request fails for a normal reason,
/// and to communicate that reason to the user without a full internal stacktrace.
/// </summary>
/// <param name="message">The message describing the reason for the request failure.</param>
/// <param name="logDetails">Additional details to be logged regarding the failure. It should be serializable to JSON.</param>
/// <param name="severity">The severity level of the message. This is only shown in internal logging.</param>
public class HandlerErrorException
(
    string message,
    object logDetails = null,
    MessageType severity = MessageType.Error
) : RpcErrorException((int)severity, logDetails!, message)
{ }
