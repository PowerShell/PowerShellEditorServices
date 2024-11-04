// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.PowerShell.EditorServices.Logging;
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

public static class LoggingBuilderExtensions
{
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

