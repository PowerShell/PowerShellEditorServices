//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices
{
    public class VariableScope
    {
        public int Id { get; private set; }

        public string Name { get; private set; }

        public VariableScope(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }
    }
}
