﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using System.Management.Automation;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    using System;

    internal class PsrlReadLine : TerminalReadLine
    {
        private readonly PSReadLineProxy _psrlProxy;

        private readonly PsesInternalHost _psesHost;

        private readonly EngineIntrinsics _engineIntrinsics;
        private IConsoleOperations _consoleOperations;

        public PsrlReadLine(
            PSReadLineProxy psrlProxy,
            PsesInternalHost psesHost,
            EngineIntrinsics engineIntrinsics,
            Func<bool, ConsoleKeyInfo> readKeyFunc,
            Action<CancellationToken> onIdleAction,
            IConsoleOperations consoleOperations)
        {
            _psrlProxy = psrlProxy;
            _psesHost = psesHost;
            _engineIntrinsics = engineIntrinsics;
            _psrlProxy.OverrideReadKey(readKeyFunc);
            _psrlProxy.OverrideIdleHandler(onIdleAction);
            _consoleOperations = consoleOperations;
        }

        public override string ReadLine(CancellationToken cancellationToken) => _psesHost.InvokeDelegate(
            representation: "ReadLine",
            new ExecutionOptions { RequiresForeground = true },
            InvokePSReadLine,
            cancellationToken);

        protected override ConsoleKeyInfo ReadKey(CancellationToken cancellationToken) => _psesHost.ReadKey(intercept: true, cancellationToken);

        protected override ConsoleKeyInfo ReadKey(CancellationToken cancellationToken)
        {
            return _consoleOperations.ReadKey(intercept: true, cancellationToken);
        }

        #endregion

        #region Private Methods

        private string InvokePSReadLine(CancellationToken cancellationToken)
        {
            EngineIntrinsics engineIntrinsics = _psesHost.IsRunspacePushed ? null : _engineIntrinsics;
            return _psrlProxy.ReadLine(_psesHost.Runspace, engineIntrinsics, cancellationToken, /* lastExecutionStatus */ null);
        }
    }
}
