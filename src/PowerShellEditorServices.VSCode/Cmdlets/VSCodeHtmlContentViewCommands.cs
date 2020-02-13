//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.VSCode.CustomViews;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.VSCode
{
    ///
    [Cmdlet(VerbsCommon.New,"VSCodeHtmlContentView")]
    [OutputType(typeof(IHtmlContentView))]
    public class NewVSCodeHtmlContentViewCommand : PSCmdlet
    {
        private HtmlContentViewsFeature _htmlContentViewsFeature;

        private ViewColumn? _showInColumn;

        ///
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Title { get; set; }

        ///
        [Parameter(Position = 1)]
        public ViewColumn ShowInColumn
        {
            get => _showInColumn.GetValueOrDefault();
            set => _showInColumn = value;
        }

        ///
        protected override void BeginProcessing()
        {
            if (_htmlContentViewsFeature == null)
            {
                if (GetVariableValue("psEditor") is EditorObject psEditor)
                {
                    _htmlContentViewsFeature = new HtmlContentViewsFeature(
                        psEditor.GetExtensionServiceProvider().LanguageServer);
                }
                else
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new ItemNotFoundException("Cannot find the '$psEditor' variable."),
                            "PSEditorNotFound",
                            ErrorCategory.ObjectNotFound,
                            targetObject: null));
                    return;
                }
            }

            IHtmlContentView view = _htmlContentViewsFeature.CreateHtmlContentViewAsync(Title)
                .GetAwaiter()
                .GetResult();

            if (_showInColumn != null) {
                try
                {
                    view.Show(_showInColumn.Value).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    WriteError(
                        new ErrorRecord(
                            e,
                            "HtmlContentViewCouldNotShow",
                            ErrorCategory.OpenError,
                            targetObject: null));

                    return;
                }
            }

            WriteObject(view);
        }
    }

    ///
    [Cmdlet(VerbsCommon.Set,"VSCodeHtmlContentView")]
    public class SetVSCodeHtmlContentViewCommand : PSCmdlet
    {
        ///
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("View")]
        [ValidateNotNull]
        public IHtmlContentView HtmlContentView { get; set; }

        ///
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("Content")]
        [AllowEmptyString]
        public string HtmlBodyContent { get; set; }

        ///
        [Parameter(Position = 2)]
        public string[] JavaScriptPaths { get; set; }

        ///
        [Parameter(Position = 3)]
        public string[] StyleSheetPaths { get; set; }

        ///
        protected override void BeginProcessing()
        {
            var htmlContent = new HtmlContent();
            htmlContent.BodyContent = HtmlBodyContent;
            htmlContent.JavaScriptPaths = JavaScriptPaths;
            htmlContent.StyleSheetPaths = StyleSheetPaths;
            try
            {
                HtmlContentView.SetContentAsync(htmlContent).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                WriteError(
                    new ErrorRecord(
                        e,
                        "HtmlContentViewCouldNotSet",
                        ErrorCategory.WriteError,
                        targetObject: null));
            }
        }
    }

    ///
    [Cmdlet(VerbsCommon.Close,"VSCodeHtmlContentView")]
    public class CloseVSCodeHtmlContentViewCommand : PSCmdlet
    {
        ///
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("View")]
        [ValidateNotNull]
        public IHtmlContentView HtmlContentView { get; set; }

        ///
        protected override void BeginProcessing()
        {
            try
            {
                HtmlContentView.Close().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                WriteError(
                    new ErrorRecord(
                        e,
                        "HtmlContentViewCouldNotClose",
                        ErrorCategory.CloseError,
                        targetObject: null));
            }
        }
    }

    ///
    [Cmdlet(VerbsCommon.Show,"VSCodeHtmlContentView")]
    public class ShowVSCodeHtmlContentViewCommand : PSCmdlet
    {
        ///
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("View")]
        [ValidateNotNull]
        public IHtmlContentView HtmlContentView { get; set; }

        ///
        [Parameter(Position = 1)]
        [Alias("Column")]
        [ValidateNotNull]
        public ViewColumn ViewColumn { get; set; } = ViewColumn.One;

        ///
        protected override void BeginProcessing()
        {
            try
            {
                HtmlContentView.Show(ViewColumn).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                WriteError(
                    new ErrorRecord(
                        e,
                        "HtmlContentViewCouldNotShow",
                        ErrorCategory.OpenError,
                        targetObject: null));
            }
        }
    }

    ///
    [Cmdlet(VerbsCommunications.Write,"VSCodeHtmlContentView")]
    public class WriteVSCodeHtmlContentViewCommand : PSCmdlet
    {
        ///
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("View")]
        [ValidateNotNull]
        public IHtmlContentView HtmlContentView { get; set; }

        ///
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 1)]
        [Alias("Content")]
        [ValidateNotNull]
        public string AppendedHtmlBodyContent { get; set; }

        ///
        protected override void ProcessRecord()
        {
            try
            {
                HtmlContentView.AppendContentAsync(AppendedHtmlBodyContent).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                WriteError(
                    new ErrorRecord(
                        e,
                        "HtmlContentViewCouldNotWrite",
                        ErrorCategory.WriteError,
                        targetObject: null));
            }
        }
    }
}
