// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Extensions.Services;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal abstract class CustomViewFeatureBase<TView>
        where TView : ICustomView
    {
        protected ILanguageServerService languageServer;

        private readonly Dictionary<Guid, TView> viewIndex;

        public CustomViewFeatureBase(
            ILanguageServerService languageServer)
        {
            this.viewIndex = new Dictionary<Guid, TView>();
            this.languageServer = languageServer;
        }

        protected void AddView(TView view)
        {
            this.viewIndex.Add(view.Id, view);
        }
    }
}
