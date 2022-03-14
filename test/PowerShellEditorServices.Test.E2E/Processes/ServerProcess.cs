// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PowerShellEditorServices.Test.E2E
{
    /// <summary>
    ///     A <see cref="ServerProcess"/> is responsible for launching or attaching to a language server, providing access to its input and output streams, and tracking its lifetime.
    /// </summary>
    public abstract class ServerProcess : IDisposable
    {
        private readonly ISubject<System.Reactive.Unit> _exitedSubject;

        private readonly Lazy<Stream> _inStreamLazy;

        private readonly Lazy<Stream> _outStreamLazy;

        /// <summary>
        ///     Create a new <see cref="ServerProcess"/>.
        /// </summary>
        /// <param name="loggerFactory">
        ///     The factory for loggers used by the process and its components.
        /// </param>
        protected ServerProcess(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            Log = LoggerFactory.CreateLogger(categoryName: GetType().FullName);

            ServerStartCompletion = new TaskCompletionSource<object>();

            ServerExitCompletion = new TaskCompletionSource<object>();
            ServerExitCompletion.SetResult(null); // Start out as if the server has already exited.

            Exited = _exitedSubject = new AsyncSubject<System.Reactive.Unit>();

            _inStreamLazy = new Lazy<Stream>(() => new LoggingStream(GetInputStream()));
            _outStreamLazy = new Lazy<Stream>(() => new LoggingStream(GetOutputStream()));
        }

        /// <summary>
        ///     Finaliser for <see cref="ServerProcess"/>.
        /// </summary>
        ~ServerProcess()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Dispose of resources being used by the launcher.
        /// </summary>
        public void Dispose() => Dispose(true);

        /// <summary>
        ///     Dispose of resources being used by the launcher.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        ///     The factory for loggers used by the process and its components.
        /// </summary>
        protected ILoggerFactory LoggerFactory { get; }

        /// <summary>
        ///     The process's logger.
        /// </summary>
        protected ILogger Log { get; }

        /// <summary>
        ///     The <see cref="TaskCompletionSource{TResult}"/> used to signal server startup.
        /// </summary>
        protected TaskCompletionSource<object> ServerStartCompletion { get; set; }

        /// <summary>
        ///     The <see cref="TaskCompletionSource{TResult}"/> used to signal server exit.
        /// </summary>
        protected TaskCompletionSource<object> ServerExitCompletion { get; set; }

        /// <summary>
        ///     Event raised when the server has exited.
        /// </summary>
        public IObservable<System.Reactive.Unit> Exited { get; }

        /// <summary>
        ///     Is the server running?
        /// </summary>
        public abstract bool IsRunning { get; }

        /// <summary>
        ///     A <see cref="Task"/> that completes when the server has started.
        /// </summary>
        public Task HasStarted => ServerStartCompletion.Task;

        /// <summary>
        ///     A <see cref="Task"/> that completes when the server has exited.
        /// </summary>
        public Task HasExited => ServerExitCompletion.Task;

        protected abstract Stream GetInputStream();

        protected abstract Stream GetOutputStream();

        /// <summary>
        ///     The server's input stream.
        /// </summary>
        /// <remarks>
        ///     The connection will write to the server's input stream, and read from its output stream.
        /// </remarks>
        public Stream InputStream => _inStreamLazy.Value;

        /// <summary>
        ///     The server's output stream.
        /// </summary>
        /// <remarks>
        ///     The connection will read from the server's output stream, and write to its input stream.
        /// </remarks>
        public Stream OutputStream => _outStreamLazy.Value;

        /// <summary>
        ///     Start or connect to the server.
        /// </summary>
        public abstract Task Start();

        /// <summary>
        ///     Stop or disconnect from the server.
        /// </summary>
        public abstract Task Stop();

        /// <summary>
        ///     Raise the <see cref="Exited"/> event.
        /// </summary>
        protected virtual void OnExited()
        {
            _exitedSubject.OnNext(System.Reactive.Unit.Default);
            _exitedSubject.OnCompleted();
        }
    }
}
