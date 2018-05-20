//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class LoggerTests
    {
        private const string testMessage = "This is a test log message.";
        private readonly string logFilePath =
            Path.Combine(
#if CoreCLR
                AppContext.BaseDirectory,
#else
                AppDomain.CurrentDomain.BaseDirectory,
#endif
                "Test.log");

        [Fact]
        public void WritesNormalLogMessage()
        {
            this.AssertWritesMessageAtLevel(LogLevel.Normal);
        }

        [Fact]
        public void WritesVerboseLogMessage()
        {
            this.AssertWritesMessageAtLevel(LogLevel.Verbose);
        }

        [Fact]
        public void WritesWarningLogMessage()
        {
            this.AssertWritesMessageAtLevel(LogLevel.Warning);
        }

        [Fact]
        public void WritesErrorLogMessage()
        {
            this.AssertWritesMessageAtLevel(LogLevel.Error);
        }

        [Fact]
        public void CanExcludeMessagesBelowNormalLevel()
        {
            this.AssertExcludesMessageBelowLevel(LogLevel.Normal);
        }

        [Fact]
        public void CanExcludeMessagesBelowWarningLevel()
        {
            this.AssertExcludesMessageBelowLevel(LogLevel.Warning);
        }

        [Fact]
        public void CanExcludeMessagesBelowErrorLevel()
        {
            this.AssertExcludesMessageBelowLevel(LogLevel.Error);
        }

        #region Helper Methods

        private void AssertWritesMessageAtLevel(LogLevel logLevel)
        {
            // Write a message at the desired level
            var logger = Logging.CreateLogger()
                            .LogLevel(LogLevel.Verbose)
                            .AddLogFile(logFilePath, useMultiprocess: true)
                            .Build();
            logger.Write(logLevel, testMessage);

            // Read the contents and verify that it's there
            string logContents = this.ReadLogContents();
            Assert.Contains(this.GetLogLevelName(logLevel), logContents);
            Assert.Contains(testMessage, logContents);
        }

        private void AssertExcludesMessageBelowLevel(LogLevel minimumLogLevel)
        {
            var logger = Logging.CreateLogger()
                            .LogLevel(minimumLogLevel)
                            .AddLogFile(logFilePath, useMultiprocess: true)
                            .Build();

            // Get all possible log levels
            LogLevel[] allLogLevels =
                Enum.GetValues(typeof(LogLevel))
                    .Cast<LogLevel>()
                    .ToArray();

            // Write a message at each log level
            foreach (var logLevel in allLogLevels)
            {
                logger.Write((LogLevel)logLevel, testMessage);
            }

            // Make sure all excluded log levels aren't in the contents
            string logContents = this.ReadLogContents();
            for (int i = 0; i < (int)minimumLogLevel; i++)
            {
                LogLevel logLevel = allLogLevels[i];
                Assert.DoesNotContain(this.GetLogLevelName(logLevel), logContents);
            }
        }

        private string GetLogLevelName(LogLevel logLevel)
        {
            return logLevel.ToString().ToUpper();
        }

        private string ReadLogContents()
        {
            return
                string.Join(
                    "\r\n",
                    File.ReadAllLines(
                        logFilePath,
                        Encoding.UTF8));
        }

        private class LogPathHelper : IDisposable
        {
            private readonly string logFilePathTemplate =
                Path.Combine(
    #if CoreCLR
                    AppContext.BaseDirectory,
    #else
                    AppDomain.CurrentDomain.BaseDirectory,
    #endif
                    "Test-{0}.log");

            public string LogFilePath { get; private set; }

            public LogPathHelper()
            {
                this.LogFilePath =
                    string.Format(
                        logFilePathTemplate,
                        "2");
                        //Guid.NewGuid().ToString());
            }

            public void Dispose()
            {
                // Delete the created file
                try
                {
                    if (this.LogFilePath != null)
                    {
                        File.Delete(this.LogFilePath);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
    }
}
