// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal interface IReadLineProvider
    {
        IReadLine ReadLine { get; }
    }

    internal class ReadLineProvider : IReadLineProvider
    {
        private readonly ILogger _logger;

        public ReadLineProvider(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger<ReadLineProvider>();

        public IReadLine ReadLine { get; private set; }

        public void OverrideReadLine(IReadLine readLine)
        {
            _logger.LogInformation($"ReadLine overridden with '{readLine.GetType()}'");
            ReadLine = readLine;
        }
    }
}
