// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;

/// <summary>
/// Extension methods for working with PSObject/PSCustomObject properties.
/// </summary>
public static class PSCustomObjectExtensions
{
    /// <summary>
    /// Gets a property value from a PSObject with the specified name and casts it to the specified type.
    /// </summary>
    public static T? GetPropertyValue<T>(this PSObject psObject, string propertyName)
    => psObject.TryGetPropertyValue(propertyName, out T? value) ? value : default;

    /// <summary>
    /// Tries to get a property value from a PSObject with the specified name and casts it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the property value to.</typeparam>
    /// <param name="psObject">The PSObject to get the property from.</param>
    /// <param name="propertyName">The name of the property to get.</param>
    /// <param name="value">When this method returns, contains the value of the property if found and converted successfully, or the default value of the type if not.</param>
    /// <returns>true if the property was found and converted successfully; otherwise, false.</returns>
    public static bool TryGetPropertyValue<T>(this PSObject psObject, string propertyName, out T? value)
    {
        value = default;

        PSPropertyInfo property = psObject.Properties[propertyName];
        if (property == null || property.Value == null)
        {
            return false;
        }
        try
        {
            object propertyValue = property.Value is PSObject valuePsObject
                ? valuePsObject.BaseObject
                : property.Value;

            // Attempt a generic cast first
            value = propertyValue is not T typedValue
            ? (T)Convert.ChangeType(propertyValue, typeof(T))
            : typedValue;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
