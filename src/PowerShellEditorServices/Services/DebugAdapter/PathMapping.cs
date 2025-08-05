// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.PowerShell.EditorServices.Services;

/// <summary>
/// Used for attach requests to map a local and remote path together.
/// </summary>
internal record PathMapping
{
    /// <summary>
    /// Gets or sets the local root of this mapping entry.
    /// </summary>
    public string? LocalRoot { get; set; }

    /// <summary>
    /// Gets or sets the remote root of this mapping entry.
    /// </summary>
    public string? RemoteRoot { get; set; }
}

#nullable disable
