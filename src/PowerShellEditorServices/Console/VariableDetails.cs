using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    public class VariableDetails
    {
        public int Id { get; private set; }

        public string Name { get; private set; }

        public string ValueString { get; private set; }

        public VariableDetails[] Children { get; private set; }

        public bool HasChildren 
        {
            get
            {
                return this.Children != null && this.Children.Length > 0;
            }
        }

        #region Public Methods

        public static VariableDetails Create(PSVariable variable)
        {
            string valueString = null;

            VariableDetails[] children = new VariableDetails[0];
            try
            {
                children = GetChildren(variable.Value);
            }
            catch (Exception e)
            {
                var mess = e.Message;
            }

            if (children.Length == 0)
            {
                valueString = GetValueString(variable.Value);
            }

            return new VariableDetails
            {
                Id = variable.Name.GetHashCode(),
                Name = variable.Name,
                ValueString = valueString,
                Children = children
            };
        }

        #endregion

        #region Private Methods

        private static VariableDetails Create(DictionaryEntry entry)
        {
            return new VariableDetails
            {
                Name = GetValueString(entry.Key),
                ValueString = GetValueString(entry.Value)
            };
        }

        private static string GetValueString(object value)
        {
            if (value != null)
            {
                return value.ToString();
            }

            return null;
        }

        private static VariableDetails Create(PSPropertyInfo property)
        {
            var propertyValue = property.Value;
            var valuePsObject = propertyValue as PSObject;
            if (valuePsObject != null && 
                !(valuePsObject.ImmediateBaseObject is string))
            {
                propertyValue = valuePsObject.ImmediateBaseObject;
            }

            return new VariableDetails
            {
                Name = property.Name,
                ValueString = GetValueString(propertyValue), // TODO: Only one or the other!
                Children = GetChildren(propertyValue)
            };
        }

        private static VariableDetails[] Create(IEnumerable enumerable)
        {
            List<VariableDetails> variables = new List<VariableDetails>();

            foreach (var item in enumerable)
            {
                try
                {
                    DictionaryEntry entry = (DictionaryEntry)item;
                    variables.Add(
                        VariableDetails.Create(
                            entry));
                }
                catch(InvalidCastException)
                {

                }
            }

            return variables.ToArray();
        }

        private static VariableDetails[] GetChildren(object obj)
        {
            List<VariableDetails> childVariables = new List<VariableDetails>();

            PSObject psObject = obj as PSObject;
            if (psObject != null)
            {
                childVariables.AddRange(
                    psObject
                        .Properties
                        .Select(p => VariableDetails.Create(p)));
            }

            IEnumerable enumerable = obj as IEnumerable;
            if (enumerable != null && !(obj is string))
            {
                childVariables.AddRange(
                    VariableDetails.Create(
                        enumerable));
            }

            return childVariables.ToArray();
        }

        #endregion
    }
}
