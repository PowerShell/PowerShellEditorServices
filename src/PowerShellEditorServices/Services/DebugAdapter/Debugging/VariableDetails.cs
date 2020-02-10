//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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

        private object valueObject;
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
                  DollarPrefix + psVariableObject.Properties["Name"].Value as string,
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
            this.valueObject = value;

            this.Id = -1; // Not been assigned a variable reference id yet
            this.Name = name;
            this.IsExpandable = GetIsExpandable(value);

            string typeName;
            this.ValueString = GetValueStringAndType(value, this.IsExpandable, out typeName);
            this.Type = typeName;
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
            VariableDetails[] childVariables = null;

            if (this.IsExpandable)
            {
                if (this.cachedChildren == null)
                {
                    this.cachedChildren = GetChildren(this.valueObject, logger);
                }

                return this.cachedChildren;
            }
            else
            {
                childVariables = Array.Empty<VariableDetails>();
            }

            return childVariables;
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
            var psobject = valueObject as PSObject;
            if (psobject != null)
            {
                valueObject = psobject.BaseObject;
            }

            Type valueType =
                valueObject != null ?
                    valueObject.GetType() :
                    null;

            TypeInfo valueTypeInfo = valueType.GetTypeInfo();

            return
                valueObject != null &&
                !valueTypeInfo.IsPrimitive &&
                !valueTypeInfo.IsEnum && // Enums don't have any properties
                !(valueObject is string) && // Strings get treated as IEnumerables
                !(valueObject is decimal) &&
                !(valueObject is UnableToRetrievePropertyMessage);
        }

        private static string GetValueStringAndType(object value, bool isExpandable, out string typeName)
        {
            string valueString = null;
            typeName = null;

            if (value == null)
            {
                // Set to identifier recognized by PowerShell to make setVariable from the debug UI more natural.
                return "$null";
            }

            Type objType = value.GetType();
            typeName = $"[{objType.FullName}]";

            if (value is bool)
            {
                // Set to identifier recognized by PowerShell to make setVariable from the debug UI more natural.
                valueString = (bool) value ? "$true" : "$false";
            }
            else if (isExpandable)
            {

                // Get the "value" for an expandable object.
                if (value is DictionaryEntry)
                {
                    // For DictionaryEntry - display the key/value as the value.
                    var entry = (DictionaryEntry)value;
                    valueString =
                        string.Format(
                            "[{0}, {1}]",
                            entry.Key,
                            GetValueStringAndType(entry.Value, GetIsExpandable(entry.Value), out typeName));
                }
                else
                {
                    string valueToString = value.SafeToString();
                    if (valueToString == null || valueToString.Equals(objType.ToString()))
                    {
                        // If the ToString() matches the type name or is null, then display the type
                        // name in PowerShell format.
                        string shortTypeName = objType.Name;

                        // For arrays and ICollection, display the number of contained items.
                        if (value is Array)
                        {
                            var arr = value as Array;
                            if (arr.Rank == 1)
                            {
                                shortTypeName = InsertDimensionSize(shortTypeName, arr.Length);
                            }
                        }
                        else if (value is ICollection)
                        {
                            var collection = (ICollection)value;
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
                if (value is string)
                {
                    valueString = "\"" + value + "\"";
                }
                else
                {
                    valueString = value.SafeToString();
                }
            }

            return valueString;
        }

        private static string InsertDimensionSize(string value, int dimensionSize)
        {
            string result = value;

            int indexLastRBracket = value.LastIndexOf("]");
            if (indexLastRBracket > 0)
            {
                result =
                    value.Substring(0, indexLastRBracket) +
                    dimensionSize +
                    value.Substring(indexLastRBracket);
            }
            else
            {
                // Types like ArrayList don't use [] in type name so
                // display value like so -  [ArrayList: 5]
                result = value + ": " + dimensionSize;
            }

            return result;
        }

        private VariableDetails[] GetChildren(object obj, ILogger logger)
        {
            List<VariableDetails> childVariables = new List<VariableDetails>();

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
                        // First add the PSObject's ETS propeties
                        childVariables.AddRange(
                            psObject
                                .Properties
                                .Where(p => p.MemberType == PSMemberTypes.NoteProperty)
                                .Select(p => new VariableDetails(p)));

                        obj = psObject.BaseObject;
                    }

                    IDictionary dictionary = obj as IDictionary;
                    IEnumerable enumerable = obj as IEnumerable;

                    // We're in the realm of regular, unwrapped .NET objects
                    if (dictionary != null)
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
                        // honors that when the object is reintepreted as an IDictionary object.
                        // FYI, a test case for this is to open $PSBoundParameters when debugging a
                        // function that defines parameters and has been passed parameters.
                        // If you open the $PSBoundParameters variable node in this scenario and see nothing,
                        // this code is broken.
                        int i = 0;
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            childVariables.Add(
                                new VariableDetails(
                                    "[" + i++ + "]",
                                    entry));
                        }
                    }
                    else if (enumerable != null && !(obj is string))
                    {
                        int i = 0;
                        foreach (var item in enumerable)
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
                logger.LogWarning($"Failed to get properties of variable {this.Name}, value invocation was attempted: {ex.Message}");
            }

            return childVariables.ToArray();
        }

        private static void AddDotNetProperties(object obj, List<VariableDetails> childVariables)
        {
            Type objectType = obj.GetType();
            var properties =
                objectType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
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
            public UnableToRetrievePropertyMessage(string message)
            {
                this.Message = message;
            }

            public string Message { get; }

            public override string ToString()
            {
                return "<" + Message + ">";
            }
        }
    }
}
