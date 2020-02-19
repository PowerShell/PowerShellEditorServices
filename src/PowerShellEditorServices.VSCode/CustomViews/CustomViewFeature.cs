//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
