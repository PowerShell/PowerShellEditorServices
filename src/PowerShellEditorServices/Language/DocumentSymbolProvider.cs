using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices
{
    internal abstract class DocumentSymbolProvider
    {
        public IEnumerable<SymbolReference> GetSymbols(ScriptFile scriptFile, Version psVersion = null)
        {
            if (CanProvideFor(scriptFile))
            {
                return GetSymbolsImpl(scriptFile, psVersion);
            }

            return Enumerable.Empty<SymbolReference>();
        }

        protected abstract IEnumerable<SymbolReference> GetSymbolsImpl(ScriptFile scriptFile, Version psVersion);

        protected abstract bool CanProvideFor(ScriptFile scriptFile);
    }
}
