// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Management.Automation.Host;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Log Level for HostLogger. This is a direct copy of LogLevel from Microsoft.Extensions.Logging, and will map to
    /// MEL.LogLevel once MEL is bootstrapped, but we don't want to load any MEL assemblies until the Assembly Load
    /// Context is set up.
    /// </summary>
    public enum PsesLogLevel
    {
        /// <summary>
        /// Logs that contain the most detailed messages. These messages may contain sensitive application data.
        /// These messages are disabled by default and should never be enabled in a production environment.
        /// </summary>
        Trace = 0,

        /// <summary>
        /// Logs that are used for interactive investigation during development.  These logs should primarily contain
        /// information useful for debugging and have no long-term value.
        /// </summary>
        Debug = 1,

        /// <summary>
        /// Logs that track the general flow of the application. These logs should have long-term value.
        /// </summary>
        Information = 2,

        /// <summary>
        /// Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the
        /// application execution to stop.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Logs that highlight when the current flow of execution is stopped due to a failure. These should indicate a
        /// failure in the current activity, not an application-wide failure.
        /// </summary>
        Error = 4,

        /// <summary>
        /// Logs that describe an unrecoverable application or system crash, or a catastrophic failure that requires
        /// immediate attention.
        /// </summary>
        Critical = 5,

        /// <summary>
        /// Not used for writing log messages. Specifies that a logging category should not write any messages.
        /// </summary>
        None = 6,
    }

    /// <summary>
    /// A logging front-end for host startup allowing handover to the backend and decoupling from
    /// the host's particular logging sink.
    /// </summary>
    /// <remarks>
    /// This custom logger exists to allow us to log during startup, which is vital information for
    /// debugging, but happens before we can load any logger library. This is because startup
    /// happens in our isolated assembly environment. See #2292 for more information.
    /// </remarks>
    public sealed class HostLogger :
        IObservable<(PsesLogLevel logLevel, string message)>,
        IObservable<(int logLevel, string message)>
    {
        /// <summary>
        /// A simple translation struct to convert PsesLogLevel to an int for backend passthrough.
        /// </summary>
        private class LogObserver : IObserver<(PsesLogLevel logLevel, string message)>
        {
            private readonly IObserver<(int logLevel, string message)> _observer;

            public LogObserver(IObserver<(int logLevel, string message)> observer) => _observer = observer;

            public void OnCompleted() => _observer.OnCompleted();

            public void OnError(Exception error) => _observer.OnError(error);

            public void OnNext((PsesLogLevel logLevel, string message) value) => _observer.OnNext(((int)value.logLevel, value.message));
        }

        /// <summary>
        /// Simple unsubscriber that allows subscribers to remove themselves from the observer list later.
        /// </summary>
        private class Unsubscriber : IDisposable
        {
            private readonly ConcurrentDictionary<IObserver<(PsesLogLevel, string)>, bool> _subscribedObservers;

            private readonly IObserver<(PsesLogLevel, string)> _thisSubscriber;

            public Unsubscriber(ConcurrentDictionary<IObserver<(PsesLogLevel, string)>, bool> subscribedObservers, IObserver<(PsesLogLevel, string)> thisSubscriber)
            {
                _subscribedObservers = subscribedObservers;
                _thisSubscriber = thisSubscriber;
            }

            public void Dispose() => _subscribedObservers.TryRemove(_thisSubscriber, out bool _);
        }

        private readonly PsesLogLevel _minimumLogLevel;

        private readonly ConcurrentQueue<(PsesLogLevel logLevel, string message)> _logMessages;

        // The bool value here is meaningless and ignored,
        // the ConcurrentDictionary just provides a way to efficiently keep track of subscribers across threads
        private readonly ConcurrentDictionary<IObserver<(PsesLogLevel logLevel, string message)>, bool> _observers;

        /// <summary>
        /// Construct a new logger in the host.
        /// </summary>
        /// <param name="minimumLogLevel">The minimum log level to log.</param>
        public HostLogger(PsesLogLevel minimumLogLevel)
        {
            _minimumLogLevel = minimumLogLevel;
            _logMessages = new ConcurrentQueue<(PsesLogLevel logLevel, string message)>();
            _observers = new ConcurrentDictionary<IObserver<(PsesLogLevel logLevel, string message)>, bool>();
        }

        /// <summary>
        /// Subscribe a new log sink.
        /// </summary>
        /// <param name="observer">The log sink to subscribe.</param>
        /// <returns>A disposable unsubscribe object.</returns>
        public IDisposable Subscribe(IObserver<(PsesLogLevel logLevel, string message)> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            _observers[observer] = true;

            // Catch up a late subscriber to messages already logged
            foreach ((PsesLogLevel logLevel, string message) entry in _logMessages)
            {
                observer.OnNext(entry);
            }

            return new Unsubscriber(_observers, observer);
        }

        /// <summary>
        /// Subscribe a new log sink.
        /// </summary>
        /// <param name="observer">The log sink to subscribe.</param>
        /// <returns>A disposable unsubscribe object.</returns>
        public IDisposable Subscribe(IObserver<(int logLevel, string message)> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return Subscribe(new LogObserver(observer));
        }

        /// <summary>
        /// Log a message to log sinks.
        /// </summary>
        /// <param name="logLevel">The log severity level of message to log.</param>
        /// <param name="message">The message to log.</param>
        public void Log(PsesLogLevel logLevel, string message)
        {
            // Do nothing if the severity is lower than the minimum
            if (logLevel < _minimumLogLevel)
            {
                return;
            }

            // Remember this for later subscriptions
            _logMessages.Enqueue((logLevel, message));

            // Send this log to all observers
            foreach (IObserver<(PsesLogLevel logLevel, string message)> observer in _observers.Keys)
            {
                observer.OnNext((logLevel, message));
            }
        }

        /// <summary>
        /// Convenience method for logging exceptions.
        /// </summary>
        /// <param name="message">The human-directed message to accompany the exception.</param>
        /// <param name="exception">The actual exception to log.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The name of the file where this is logged.</param>
        /// <param name="callerLineNumber">The line in the file where this is logged.</param>
        public void LogException(
            string message,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = -1) => Log(PsesLogLevel.Error, $"{message}. Exception logged in {callerSourceFile} on line {callerLineNumber} in {callerName}:\n{exception}");
    }

    /// <summary>
    /// A log sink to direct log messages back to the PowerShell host.
    /// </summary>
    /// <remarks>
    /// Note that calling this through the cmdlet causes an error,
    /// so instead we log directly to the host.
    /// Since it's likely that the process will end when PSES shuts down,
    /// there's no good reason to need objects rather than writing directly to the host.
    /// </remarks>
    /// <param name="ui">The PowerShell host user interface object to log output to.</param>
    internal class PSHostLogger(PSHostUserInterface ui) : IObserver<(PsesLogLevel logLevel, string message)>
    {

        public void OnCompleted()
        {
            // No-op since there's nothing to close or dispose,
            // we just stop writing to the host
        }

        public void OnError(Exception error) => OnNext((PsesLogLevel.Error, $"Error occurred while logging: {error}"));

        public void OnNext((PsesLogLevel logLevel, string message) value)
        {
            (PsesLogLevel logLevel, string message) = value;
            switch (logLevel)
            {
                case PsesLogLevel.Trace:
                case PsesLogLevel.Debug:
                    ui.WriteDebugLine(message);
                    break;

                case PsesLogLevel.Information:
                    ui.WriteVerboseLine(message);
                    break;

                case PsesLogLevel.Warning:
                    ui.WriteWarningLine(message);
                    break;

                case PsesLogLevel.Error:
                case PsesLogLevel.Critical:
                    ui.WriteErrorLine(message);
                    break;

                default:
                    ui.WriteLine(message);
                    break;
            }
        }
    }

    /// <summary>
    /// A simple log sink that logs to a stream, typically used to log to a file.
    /// </summary>
    internal class StreamLogger : IObserver<(PsesLogLevel logLevel, string message)>, IDisposable
    {
        public static StreamLogger CreateWithNewFile(string path)
        {
            FileStream fileStream = new(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            return new StreamLogger(new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)));
        }

        private readonly StreamWriter _fileWriter;

        private readonly BlockingCollection<string> _messageQueue;

        private readonly CancellationTokenSource _cancellationSource;

        private readonly Thread _writerThread;

        // This cannot be a bool
        // See https://stackoverflow.com/q/6164751
        private int _hasCompleted;

        private IDisposable _unsubscriber;

        public StreamLogger(StreamWriter streamWriter)
        {
            streamWriter.AutoFlush = true;
            _fileWriter = streamWriter;
            _hasCompleted = 0;
            _cancellationSource = new CancellationTokenSource();
            _messageQueue = new BlockingCollection<string>();

            // Start writer listening to queue
            _writerThread = new Thread(RunWriter)
            {
                Name = "PSES Stream Logger Thread",
            };
            _writerThread.Start();
        }

        public void OnCompleted()
        {
            // Ensure we only complete once
            if (Interlocked.Exchange(ref _hasCompleted, 1) != 0)
            {
                return;
            }

            _cancellationSource.Cancel();
            _writerThread.Join();
            _unsubscriber.Dispose();
            _fileWriter.Flush();
            _fileWriter.Close();
            _fileWriter.Dispose();
            _cancellationSource.Dispose();
            _messageQueue.Dispose();
        }

        public void OnError(Exception error) => OnNext((PsesLogLevel.Error, $"Error occurred while logging: {error}"));

        public void OnNext((PsesLogLevel logLevel, string message) value)
        {
            string message = value.logLevel switch
            {
                // String interpolation often considered a logging sin is OK here because our filtering happens before.
                PsesLogLevel.Trace => $"[TRC]: {value.message}",
                PsesLogLevel.Debug => $"[DBG]: {value.message}",
                PsesLogLevel.Information => $"[INF]: {value.message}",
                PsesLogLevel.Warning => $"[WRN]: {value.message}",
                PsesLogLevel.Error => $"[ERR]: {value.message}",
                PsesLogLevel.Critical => $"[CRT]: {value.message}",
                _ => value.message,
            };

            _messageQueue.Add(message);
        }

        public void AddUnsubscriber(IDisposable unsubscriber) => _unsubscriber = unsubscriber;

        public void Dispose() => OnCompleted();

        private void RunWriter()
        {
            try
            {
                foreach (string logMessage in _messageQueue.GetConsumingEnumerable(_cancellationSource.Token))
                {
                    _fileWriter.WriteLine(logMessage);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
