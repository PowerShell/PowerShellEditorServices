// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        #region Constructors

        public PsrlReadLine(
            PSReadLineProxy psrlProxy,
            PsesInternalHost psesHost,
            EngineIntrinsics engineIntrinsics,
            Func<bool, ConsoleKeyInfo> readKeyFunc,
            Action<CancellationToken> onIdleAction)
        {
            _psrlProxy = psrlProxy;
            _psesHost = psesHost;
            _engineIntrinsics = engineIntrinsics;
            _psrlProxy.OverrideReadKey(readKeyFunc);
            _psrlProxy.OverrideIdleHandler(onIdleAction);
        }

        #endregion

        #region Public Methods

        public override string ReadLine(CancellationToken cancellationToken)
        {
            return _psesHost.InvokeDelegate<string>(representation: "ReadLine", new ExecutionOptions { MustRunInForeground = true }, InvokePSReadLine, cancellationToken);
        }

        protected override ConsoleKeyInfo ReadKey(CancellationToken cancellationToken)
        {
            return ConsoleProxy.ReadKey(intercept: true, cancellationToken);
        }

        #endregion

        #region Private Methods

        private string InvokePSReadLine(CancellationToken cancellationToken)
        {
            EngineIntrinsics engineIntrinsics = _psesHost.IsRunspacePushed ? null : _engineIntrinsics;
            return _psrlProxy.ReadLine(_psesHost.Runspace, engineIntrinsics, cancellationToken, /* lastExecutionStatus */ null);
        }

        #endregion
    }
}
