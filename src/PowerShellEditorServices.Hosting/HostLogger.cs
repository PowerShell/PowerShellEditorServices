//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Management.Automation.Host;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// User-facing log level for editor services configuration.
    /// </summary>
    public enum PsesLogLevel
    {
        Diagnostic = 0,
        Verbose = 1,
        Normal = 2,
        Warning = 3,
        Error = 4,
    }

    /// <summary>
    /// A logging front-end for host startup allowing handover to the backend
    /// and decoupling from the host's particular logging sink.
    /// </summary>
    public class HostLogger :
        IObservable<(PsesLogLevel logLevel, string message)>,
        IObservable<(int logLevel, string message)>
    {
        /// <summary>
        /// A simple translation struct to convert PsesLogLevel to an int for backend passthrough.
        /// </summary>
        private struct LogObserver : IObserver<(PsesLogLevel logLevel, string message)>
        {
            private readonly IObserver<(int logLevel, string message)> _observer;

            public LogObserver(IObserver<(int logLevel, string message)> observer)
            {
                _observer = observer;
            }

            public void OnCompleted()
            {
                _observer.OnCompleted();
            }

            public void OnError(Exception error)
            {
                _observer.OnError(error);
            }

            public void OnNext((PsesLogLevel logLevel, string message) value)
            {
                _observer.OnNext(((int)value.logLevel, value.message));
            }
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

            public void Dispose()
            {
                _subscribedObservers.TryRemove(_thisSubscriber, out bool _);
            }
        }

        private readonly PsesLogLevel _minimumLogLevel;

        private readonly ConcurrentQueue<(PsesLogLevel logLevel, string message)> _logMessages;

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
        /// <param name="exception">The actual execption to log.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The name of the file where this is logged.</param>
        /// <param name="callerLineNumber">The line in the file where this is logged.</param>
        public void LogException(
            string message,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = -1)
        {
            Log(PsesLogLevel.Error, $"{message}. Exception logged in {callerSourceFile} on line {callerLineNumber} in {callerName}:\n{exception}");
        }

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
    internal class PSHostLogger : IObserver<(PsesLogLevel logLevel, string message)>
    {
        private readonly PSHostUserInterface _ui;

        /// <summary>
        /// Create a new PowerShell host logger.
        /// </summary>
        /// <param name="ui">The PowerShell host user interface object to log output to.</param>
        public PSHostLogger(PSHostUserInterface ui)
        {
            _ui = ui;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            OnNext((PsesLogLevel.Error, $"Error occurred while logging: {error}"));
        }

        public void OnNext((PsesLogLevel logLevel, string message) value)
        {
            switch (value.logLevel)
            {
                case PsesLogLevel.Diagnostic:
                    _ui.WriteDebugLine(value.message);
                    return;

                case PsesLogLevel.Verbose:
                    _ui.WriteVerboseLine(value.message);
                    return;

                case PsesLogLevel.Normal:
                    _ui.WriteLine(value.message);
                    return;

                case PsesLogLevel.Warning:
                    _ui.WriteWarningLine(value.message);
                    return;

                case PsesLogLevel.Error:
                    _ui.WriteErrorLine(value.message);
                    return;
            }
        }
    }
}
