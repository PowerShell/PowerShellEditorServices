// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Microsoft.PowerShell.EditorServices.Logging;

internal class LanguageServerLogger(ILanguageServerFacade responseRouter, string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Disposable.Empty;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (responseRouter is null)
        {
            throw new InvalidOperationException("Log received without a valid responseRouter dependency. This is a bug, please report it.");
        }
        // Any Omnisharp or trace logs are directly LSP protocol related and we send them to the trace channel
        // TODO: Dynamically adjust if SetTrace is reported
        // BUG: There is an omnisharp filter incorrectly filtering this. As a workaround we will use logMessage for now.
        //  https://github.com/OmniSharp/csharp-language-server-protocol/issues/1390
        //
        // {
        //     // Everything with omnisharp goes directly to trace
        //     string eventMessage = string.Empty;
        //     string exceptionName = exception?.GetType().Name ?? string.Empty;
        //     if (eventId.Name is not null)
        //     {
        //         eventMessage = eventId.Id == 0 ? eventId.Name : $"{eventId.Name} [{eventId.Id}] ";
        //     }

        //     LogTraceParams trace = new()
        //     {
        //         Message = categoryName + ": " + eventMessage + exceptionName,
        //         Verbose = formatter(state, exception)
        //     };
        //     responseRouter.Client.LogTrace(trace);
        // }

        // Drop all omnisharp messages to trace. This isn't a MEL filter because it's specific only to this provider.
        if (categoryName.StartsWith("OmniSharp.", StringComparison.OrdinalIgnoreCase))
        {
            logLevel = LogLevel.Trace;
        }

        (MessageType messageType, string messagePrepend) = GetMessageInfo(logLevel);

        // The vscode-languageserver-node client doesn't support LogOutputChannel as of 2024-11-24 and also doesn't
        // provide a way to middleware the incoming log messages, so our output channel has no idea what the logLevel
        // is. As a workaround, we send the severity in-line with the message for the client to parse.
        // BUG: https://github.com/microsoft/vscode-languageserver-node/issues/1116
        if (responseRouter.Client?.ClientSettings?.ClientInfo?.Name == "Visual Studio Code")
        {
            messagePrepend = logLevel switch
            {
                LogLevel.Critical => "<Error>CRITICAL: ",
                LogLevel.Error => "<Error>",
                LogLevel.Warning => "<Warning>",
                LogLevel.Information => "<Info>",
                LogLevel.Debug => "<Debug>",
                LogLevel.Trace => "<Trace>",
                _ => string.Empty
            };

            // The vscode formatter prepends some extra stuff to Info specifically, so we drop Info to Log, but it will get logged correctly on the other side thanks to our inline indicator that our custom parser on the other side will pick up and process.
            if (messageType == MessageType.Info)
            {
                messageType = MessageType.Log;
            }
        }

        LogMessageParams logMessage = new()
        {
            Type = messageType,
            Message = messagePrepend + categoryName + ": " + formatter(state, exception) +
                (exception != null ? " - " + exception : "") + " | " +
                //Hopefully this isn't too expensive in the long run
                FormatState(state, exception)
        };
        responseRouter.Window.Log(logMessage);
    }

    /// <summary>
    /// Formats the state object into a string for logging.
    /// </summary>
    /// <remarks>
    /// This is copied from Omnisharp, we can probably do better.
    /// </remarks>
    /// <typeparam name="TState"></typeparam>
    /// <param name="state"></param>
    /// <param name="exception"></param>
    /// <returns></returns>
    private static string FormatState<TState>(TState state, Exception? exception)
    {
        return state switch
        {
            IEnumerable<KeyValuePair<string, object>> dict => string.Join(" ", dict.Where(z => z.Key != "{OriginalFormat}").Select(z => $"{z.Key}='{z.Value}'")),
            _ => JsonConvert.SerializeObject(state).Replace("\"", "'")
        };
    }

    /// <summary>
    /// Maps MEL log levels to LSP message types
    /// </summary>
    private static (MessageType messageType, string messagePrepend) GetMessageInfo(LogLevel logLevel)
        => logLevel switch
        {
            LogLevel.Critical => (MessageType.Error, "Critical: "),
            LogLevel.Error => (MessageType.Error, string.Empty),
            LogLevel.Warning => (MessageType.Warning, string.Empty),
            LogLevel.Information => (MessageType.Info, string.Empty),
            LogLevel.Debug => (MessageType.Log, string.Empty),
            LogLevel.Trace => (MessageType.Log, "Trace: "),
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
}

internal class LanguageServerLoggerProvider(ILanguageServerFacade languageServer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new LanguageServerLogger(languageServer, categoryName);

    public void Dispose() { }
}


public static class LanguageServerLoggerExtensions
{
    /// <summary>
    /// Adds a custom logger provider for PSES LSP, that provides more granular categorization than the default Omnisharp logger, such as separating Omnisharp and PSES messages to different channels.
    /// </summary>
    public static ILoggingBuilder AddPsesLanguageServerLogging(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, LanguageServerLoggerProvider>();
        return builder;
    }

    public static ILoggingBuilder AddLspClientConfigurableMinimumLevel(
        this ILoggingBuilder builder,
        LogLevel initialLevel = LogLevel.Trace
    )
    {
        builder.Services.AddOptions<LoggerFilterOptions>();
        builder.Services.AddSingleton<DynamicLogLevelOptions>(sp =>
        {
            IOptionsMonitor<LoggerFilterOptions> optionsMonitor = sp.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>();
            return new(initialLevel, optionsMonitor);
        });
        builder.Services.AddSingleton<IConfigureOptions<LoggerFilterOptions>>(sp =>
            sp.GetRequiredService<DynamicLogLevelOptions>());

        return builder;
    }
}

internal class DynamicLogLevelOptions(
    LogLevel initialLevel,
    IOptionsMonitor<LoggerFilterOptions> optionsMonitor) : IConfigureOptions<LoggerFilterOptions>
{
    private LogLevel _currentLevel = initialLevel;
    private readonly IOptionsMonitor<LoggerFilterOptions> _optionsMonitor = optionsMonitor;

    public void Configure(LoggerFilterOptions options) => options.MinLevel = _currentLevel;

    public void SetLogLevel(LogLevel level)
    {
        _currentLevel = level;
        // Trigger reload of options to apply new log level
        _optionsMonitor.CurrentValue.MinLevel = level;
    }
}
