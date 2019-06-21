#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Search-Foo {
    param ()
    "success"
}

Set-Alias sfoo Search-Foo

Export-ModuleMember -Function Search-Foo -Alias sfoo
