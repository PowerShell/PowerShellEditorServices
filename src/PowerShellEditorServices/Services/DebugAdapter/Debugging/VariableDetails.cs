// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Contains details pertaining to a variable in the current
    /// debugging session.
    /// </summary>
    [DebuggerDisplay("Name = {Name}, Id = {Id}, Value = {ValueString}")]
    internal class VariableDetails : VariableDetailsBase
    {
        #region Fields

        /// <summary>
        /// Provides a constant for the dollar sign variable prefix string.
        /// </summary>
        public const string DollarPrefix = "$";
        protected object ValueObject { get; }
        private VariableDetails[] cachedChildren;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// the details contained in a PSVariable instance.
        /// </summary>
        /// <param name="psVariable">
        /// The PSVariable instance from which variable details will be obtained.
        /// </param>
        public VariableDetails(PSVariable psVariable)
            : this(DollarPrefix + psVariable.Name, psVariable.Value)
        {
        }

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// the name and value pair stored inside of a PSObject which
        /// represents a PSVariable.
        /// </summary>
        /// <param name="psVariableObject">
        /// The PSObject which represents a PSVariable.
        /// </param>
        public VariableDetails(PSObject psVariableObject)
            : this(
                  DollarPrefix + psVariableObject.Properties["Name"].Value,
                  psVariableObject.Properties["Value"].Value)
        {
        }

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// the details contained in a PSPropertyInfo instance.
        /// </summary>
        /// <param name="psProperty">
        /// The PSPropertyInfo instance from which variable details will be obtained.
        /// </param>
        public VariableDetails(PSPropertyInfo psProperty)
            : this(psProperty.Name, psProperty.Value)
        {
        }

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// a given name/value pair.
        /// </summary>
        /// <param name="name">The variable's name.</param>
        /// <param name="value">The variable's value.</param>
        public VariableDetails(string name, object value)
        {
            ValueObject = value;

            Id = -1; // Not been assigned a variable reference id yet
            Name = name;
            IsExpandable = GetIsExpandable(value);

            ValueString = GetValueStringAndType(value, IsExpandable, out string typeName);
            Type = typeName;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// If this variable instance is expandable, this method returns the
        /// details of its children.  Otherwise it returns an empty array.
        /// </summary>
        /// <returns></returns>
        public override VariableDetailsBase[] GetChildren(ILogger logger)
        {
            if (IsExpandable)
            {
                if (cachedChildren == null)
                {
                    cachedChildren = GetChildren(ValueObject, logger);
                }

                return cachedChildren;
            }

            return Array.Empty<VariableDetails>();
        }

        #endregion

        #region Private Methods

        private static bool GetIsExpandable(object valueObject)
        {
            if (valueObject == null)
            {
                return false;
            }

            // If a PSObject, unwrap it
            if (valueObject is PSObject psobject)
            {
                valueObject = psobject.BaseObject;
            }

            Type valueType =
                valueObject?.GetType();

            TypeInfo valueTypeInfo = valueType.GetTypeInfo();

            return
                valueObject != null &&
                !valueTypeInfo.IsPrimitive &&
                !valueTypeInfo.IsEnum && // Enums don't have any properties
                valueObject is not string && // Strings get treated as IEnumerables
                valueObject is not decimal &&
                valueObject is not UnableToRetrievePropertyMessage;
        }

        private static string GetValueStringAndType(object value, bool isExpandable, out string typeName)
        {
            typeName = null;

            if (value == null)
            {
                // Set to identifier recognized by PowerShell to make setVariable from the debug UI more natural.
                return "$null";
            }

            Type objType = value.GetType();

            // This is the type format PowerShell users expect and will appear when you hover a variable name
            typeName = '[' + objType.FullName + ']';

            string valueString;
            if (value is bool x)
            {
                // Set to identifier recognized by PowerShell to make setVariable from the debug UI more natural.
                valueString = x ? "$true" : "$false";

                // We need to use this "magic value" to highlight in vscode properly
                // These "magic values" are analogous to TypeScript and are visible in VSCode here:
                // https://github.com/microsoft/vscode/blob/57ca9b99d5b6a59f2d2e0f082ae186559f45f1d8/src/vs/workbench/contrib/debug/browser/baseDebugView.ts#L68-L78
                // NOTE: we don't do numbers and strings since they (so far) seem to get detected properly by
                // serialization, and the original .NET type can be preserved so it shows up in the variable name
                // type hover as the original .NET type.
                typeName = "boolean";
            }
            else if (isExpandable)
            {
                // For DictionaryEntry - display the key/value as the value.
                // Get the "value" for an expandable object.
                if (value is DictionaryEntry entry)
                {
                    valueString = GetValueStringAndType(entry.Value, GetIsExpandable(entry.Value), out typeName);
                }
                else
                {
                    string valueToString = value.SafeToString();
                    if (valueToString?.Equals(objType.ToString()) != false)
                    {
                        // If the ToString() matches the type name or is null, then display the type
                        // name in PowerShell format.
                        string shortTypeName = objType.Name;

                        // For arrays and ICollection, display the number of contained items.
                        if (value is Array)
                        {
                            Array arr = value as Array;
                            if (arr.Rank == 1)
                            {
                                shortTypeName = InsertDimensionSize(shortTypeName, arr.Length);
                            }
                        }
                        else if (value is ICollection collection)
                        {
                            shortTypeName = InsertDimensionSize(shortTypeName, collection.Count);
                        }

                        valueString = $"[{shortTypeName}]";
                    }
                    else
                    {
                        valueString = valueToString;
                    }
                }
            }
            else
            {
                // Value is a scalar (not expandable). If it's a string, display it directly otherwise use SafeToString()
                valueString = value is string ? "\"" + value + "\"" : value.SafeToString();
            }

            return valueString;
        }

        private static string InsertDimensionSize(string value, int dimensionSize)
        {
            int indexLastRBracket = value.LastIndexOf("]");
            if (indexLastRBracket > 0)
            {
                return value.Substring(0, indexLastRBracket) +
                    dimensionSize +
                    value.Substring(indexLastRBracket);
            }
            // Types like ArrayList don't use [] in type name so
            // display value like so -  [ArrayList: 5]
            return value + ": " + dimensionSize;
        }

        private VariableDetails[] GetChildren(object obj, ILogger logger)
        {
            List<VariableDetails> childVariables = new();

            if (obj == null)
            {
                return childVariables.ToArray();
            }

            try
            {
                PSObject psObject = obj as PSObject;

                if ((psObject != null) &&
                    (psObject.TypeNames[0] == typeof(PSCustomObject).ToString()))
                {
                    // PowerShell PSCustomObject's properties are completely defined by the ETS type system.
                    childVariables.AddRange(
                        psObject
                            .Properties
                            .Select(p => new VariableDetails(p)));
                }
                else
                {
                    // If a PSObject other than a PSCustomObject, unwrap it.
                    if (psObject != null)
                    {
                        // First add the PSObject's ETS properties
                        childVariables.AddRange(
                            psObject
                                .Properties
                                // Here we check the object's MemberType against the `Properties`
                                // bit-mask to determine if this is a property. Hence the selection
                                // will only include properties.
                                .Where(p => (PSMemberTypes.Properties & p.MemberType) is not 0)
                                .Select(p => new VariableDetails(p)));

                        obj = psObject.BaseObject;
                    }

                    // We're in the realm of regular, unwrapped .NET objects
                    if (obj is IDictionary dictionary)
                    {
                        // Buckle up kids, this is a bit weird.  We could not use the LINQ
                        // operator OfType<DictionaryEntry>.  Even though R# will squiggle the
                        // "foreach" keyword below and offer to convert to a LINQ-expression - DON'T DO IT!
                        // The reason is that LINQ extension methods work with objects of type
                        // IEnumerable.  Objects of type Dictionary<,>, respond to iteration via
                        // IEnumerable by returning KeyValuePair<,> objects.  Unfortunately non-generic
                        // dictionaries like HashTable return DictionaryEntry objects.
                        // It turns out that iteration via C#'s foreach loop, operates on the variable's
                        // type which in this case is IDictionary.  IDictionary was designed to always
                        // return DictionaryEntry objects upon iteration and the Dictionary<,> implementation
                        // honors that when the object is reinterpreted as an IDictionary object.
                        // FYI, a test case for this is to open $PSBoundParameters when debugging a
                        // function that defines parameters and has been passed parameters.
                        // If you open the $PSBoundParameters variable node in this scenario and see nothing,
                        // this code is broken.
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            childVariables.Add(
                                new VariableDetails(
                                    "[" + entry.Key + "]",
                                    entry));
                        }
                    }
                    else if (obj is IEnumerable enumerable and not string)
                    {
                        int i = 0;
                        foreach (object item in enumerable)
                        {
                            childVariables.Add(
                                new VariableDetails(
                                    "[" + i++ + "]",
                                    item));
                        }
                    }

                    AddDotNetProperties(obj, childVariables);
                }
            }
            catch (GetValueInvocationException ex)
            {
                // This exception occurs when accessing the value of a
                // variable causes a script to be executed.  Right now
                // we aren't loading children on the pipeline thread so
                // this causes an exception to be raised.  In this case,
                // just return an empty list of children.
                logger.LogWarning($"Failed to get properties of variable {Name}, value invocation was attempted: {ex.Message}");
            }

            return childVariables.ToArray();
        }

        protected static void AddDotNetProperties(object obj, List<VariableDetails> childVariables, bool noRawView = false)
        {
            Type objectType = obj.GetType();

            // For certain array or dictionary types, we want to hide additional properties under a "raw view" header
            // to reduce noise. This is inspired by the C# vscode extension.
            if (!noRawView && obj is IEnumerable)
            {
                childVariables.Add(new VariableDetailsRawView(obj));
                return;
            }

            PropertyInfo[] properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                // Don't display indexer properties, it causes an exception anyway.
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                try
                {
                    childVariables.Add(
                        new VariableDetails(
                            property.Name,
                            property.GetValue(obj)));
                }
                catch (Exception ex)
                {
                    // Some properties can throw exceptions, add the property
                    // name and info about the error.
                    if (ex is TargetInvocationException)
                    {
                        ex = ex.InnerException;
                    }

                    childVariables.Add(
                        new VariableDetails(
                            property.Name,
                            new UnableToRetrievePropertyMessage(
                                "Error retrieving property - " + ex.GetType().Name)));
                }
            }
        }

        #endregion

        private struct UnableToRetrievePropertyMessage
        {
            public UnableToRetrievePropertyMessage(string message) => Message = message;

            public string Message { get; }

            public override string ToString() => "<" + Message + ">";
        }
    }

    /// <summary>
    /// A VariableDetails that only returns the raw view properties of the object, rather than its values.
    /// </summary>
    internal sealed class VariableDetailsRawView : VariableDetails
    {
        private const string RawViewName = "Raw View";

        public VariableDetailsRawView(object value) : base(RawViewName, value)
        {
            ValueString = "";
            Type = "";
        }

        public override VariableDetailsBase[] GetChildren(ILogger logger)
        {
            List<VariableDetails> childVariables = new();
            AddDotNetProperties(ValueObject, childVariables, noRawView: true);
            return childVariables.ToArray();
        }
    }
}
