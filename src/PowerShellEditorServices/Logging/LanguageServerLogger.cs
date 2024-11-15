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
        // Any Omnisharp or trace logs are directly LSP protocol related and we send them to the trace channel
        // TODO: Dynamically adjust if SetTrace is reported
        // BUG: There is an omnisharp filter incorrectly filtering this. As a workaround we will use logMessage.
        //  https://github.com/OmniSharp/csharp-language-server-protocol/issues/1390
        // if (categoryName.StartsWith("OmniSharp") || logLevel == LogLevel.Trace)
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
        if (TryGetMessageType(logLevel, out MessageType messageType))
        {
            LogMessageParams logMessage = new()
            {
                Type = messageType,
                // TODO: Add Critical and Debug delineations
                Message = categoryName + ": " + formatter(state, exception) +
                    (exception != null ? " - " + exception : "") + " | " +
                    //Hopefully this isn't too expensive in the long run
                    FormatState(state, exception)
            };
            responseRouter.Window.Log(logMessage);
        }
    }


    private static string FormatState<TState>(TState state, Exception? exception)
    {
        return state switch
        {
            IEnumerable<KeyValuePair<string, object>> dict => string.Join(" ", dict.Where(z => z.Key != "{OriginalFormat}").Select(z => $"{z.Key}='{z.Value}'")),
            _ => JsonConvert.SerializeObject(state).Replace("\"", "'")
        };
    }

    private static bool TryGetMessageType(LogLevel logLevel, out MessageType messageType)
    {
        switch (logLevel)
        {
            case LogLevel.Critical:
            case LogLevel.Error:
                messageType = MessageType.Error;
                return true;
            case LogLevel.Warning:
                messageType = MessageType.Warning;
                return true;
            case LogLevel.Information:
                messageType = MessageType.Info;
                return true;
            case LogLevel.Debug:
            case LogLevel.Trace:
                messageType = MessageType.Log;
                return true;
        }

        messageType = MessageType.Log;
        return false;
    }
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
