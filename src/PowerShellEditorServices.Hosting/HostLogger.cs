using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public enum PsesLogLevel
    {
        Diagnostic = 0,
        Verbose = 1,
        Normal = 2,
        Warning = 3,
        Error = 4,
    }

    public class HostLogger :
        IObservable<(PsesLogLevel logLevel, string message)>,
        IObservable<(int logLevel, string message)>
    {
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

        private struct Unsubscriber : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private readonly PsesLogLevel _minimumLogLevel;

        private readonly ConcurrentQueue<(PsesLogLevel logLevel, string message)> _logMessages;

        private readonly ConcurrentBag<IObserver<(PsesLogLevel logLevel, string message)>> _observers;

        public HostLogger(PsesLogLevel minimumLogLevel)
        {
            _minimumLogLevel = minimumLogLevel;
            _logMessages = new ConcurrentQueue<(PsesLogLevel logLevel, string message)>();
            _observers = new ConcurrentBag<IObserver<(PsesLogLevel logLevel, string message)>>();
        }

        public IDisposable Subscribe(IObserver<(PsesLogLevel logLevel, string message)> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            _observers.Add(observer);

            foreach ((PsesLogLevel logLevel, string message) entry in _logMessages)
            {
                observer.OnNext(entry);
            }

            return new Unsubscriber();
        }

        public IDisposable Subscribe(IObserver<(int logLevel, string message)> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return Subscribe(new LogObserver(observer));
        }

        public void Log(PsesLogLevel logLevel, string message)
        {
            if (logLevel < _minimumLogLevel)
            {
                return;
            }

            _logMessages.Enqueue((logLevel, message));
            foreach (IObserver<(PsesLogLevel logLevel, string message)> observer in _observers)
            {
                observer.OnNext((logLevel, message));
            }
        }

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

    internal class PSHostLogger : IObserver<(PsesLogLevel logLevel, string message)>
    {
        private readonly PSHostUserInterface _ui;

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
