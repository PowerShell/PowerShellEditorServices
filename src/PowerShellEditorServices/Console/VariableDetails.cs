using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Console
{
    public class VariableDetails
    {
        #region Fields

        public const int LocalScopeVariableId = 1;
        public const int GlobalScopeVariableId = 2;
        public const int FirstVariableId = 10;

        private object valueObject;
        private VariableDetails[] cachedChildren;

        #endregion

        #region Properties

        public int Id { get; set; }

        public string Name { get; private set; }

        public string ValueString { get; private set; }

        public bool IsExpandable { get; private set; }

        #endregion

        #region Constructors

        public VariableDetails(PSVariable psVariable)
            : this(psVariable.Name, psVariable.Value)
        {
        }

        public VariableDetails(PSPropertyInfo psProperty)
            : this(psProperty.Name, psProperty.Value)
        {
        }

        public VariableDetails(string name, object value)
        {
            this.valueObject = value;

            this.IsExpandable = GetIsExpandable(value);
            this.Name = name;
            this.ValueString =
                this.IsExpandable == false ?
                    GetValueString(value) :
                    " "; // An empty string isn't enough due to a temporary bug in VS Code.
        }

        #endregion

        #region Public Methods

        public VariableDetails[] GetChildren()
        {
            VariableDetails[] childVariables = null;

            if (this.IsExpandable)
            {
                if (this.cachedChildren == null)
                {
                    this.cachedChildren = GetChildren(this.valueObject);
                }

                return this.cachedChildren;
            }
            else
            {
                childVariables = new VariableDetails[0];
            }

            return childVariables;
        }

        #endregion

        #region Private Methods

        private static bool GetIsExpandable(object valueObject)
        {
            Type valueType = 
                valueObject != null ? 
                    valueObject.GetType() : 
                    null;

            return
                valueObject != null &&
                !valueType.IsValueType &&
                !(valueObject is string); // Strings get treated as IEnumerables
        }

        private static string GetValueString(object value)
        {
            return
                value != null ?
                    value.ToString() :
                    "null";
        }

        private static VariableDetails[] GetChildren(object obj)
        {
            List<VariableDetails> childVariables = new List<VariableDetails>();

            PSObject psObject = obj as PSObject;
            IDictionary dictionary = obj as IDictionary;
            IEnumerable enumerable = obj as IEnumerable;

            if (psObject != null)
            {
                childVariables.AddRange(
                    psObject
                        .Properties
                        .Select(p => new VariableDetails(p)));
            }
            else if (dictionary != null)
            {
                childVariables.AddRange(
                    dictionary
                        .OfType<DictionaryEntry>()
                        .Select(e => new VariableDetails(e.Key.ToString(), e.Value)));
            }
            else if (enumerable != null && !(obj is string))
            {
                int i = 0;
                foreach (var item in enumerable)
                {
                    childVariables.Add(
                        new VariableDetails(
                            string.Format("[{0}]", i),
                            item));

                    i++;
                }
            }
            else if (obj != null)
            {
                // Object must be a normal .NET type, pull all of its
                // properties and their values
                Type objectType = obj.GetType();
                var properties = 
                    objectType.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    try
                    {
                        childVariables.Add(
                            new VariableDetails(
                                property.Name,
                                property.GetValue(obj)));
                    }
                    catch (Exception)
                    {
                        // Some properties can throw exceptions, add the property
                        // name and empty string
                        childVariables.Add(
                            new VariableDetails(
                                property.Name,
                                string.Empty));
                    }
                }
            }

            return childVariables.ToArray();
        }

        #endregion
    }
}
