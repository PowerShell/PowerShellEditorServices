//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides common validation methods to simplify method
    /// parameter checks.
    /// </summary>
    public static class Validate
    {
        /// <summary>
        /// Throws ArgumentNullException if value is null.
        /// </summary>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <param name="valueToCheck">The value of the parameter being validated.</param>
        public static void IsNotNull(string parameterName, object valueToCheck)
        {
            if (valueToCheck == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the value is outside 
        /// of the given lower and upper limits.
        /// </summary>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <param name="valueToCheck">The value of the parameter being validated.</param>
        /// <param name="lowerLimit">The lower limit which the value should not be less than.</param>
        /// <param name="upperLimit">The upper limit which the value should not be greater than.</param>
        public static void IsWithinRange(
            string parameterName,
            int valueToCheck,
            int lowerLimit, 
            int upperLimit)
        {
            // TODO: Debug assert here if lowerLimit >= upperLimit

            if (valueToCheck < lowerLimit || valueToCheck > upperLimit)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    valueToCheck,
                    string.Format(
                        "Value is not between {0} and {1}",
                        lowerLimit,
                        upperLimit));
            }
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the value is greater than or equal 
        /// to the given upper limit.
        /// </summary>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <param name="valueToCheck">The value of the parameter being validated.</param>
        /// <param name="upperLimit">The upper limit which the value should be less than.</param>
        public static void IsLessThan(
            string parameterName,
            int valueToCheck,
            int upperLimit)
        {
            if (valueToCheck >= upperLimit)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    valueToCheck,
                    string.Format(
                        "Value is greater than or equal to {0}",
                        upperLimit));
            }
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the value is less than or equal 
        /// to the given lower limit.
        /// </summary>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <param name="valueToCheck">The value of the parameter being validated.</param>
        /// <param name="lowerLimit">The lower limit which the value should be greater than.</param>
        public static void IsGreaterThan(
            string parameterName,
            int valueToCheck,
            int lowerLimit)
        {
            if (valueToCheck < lowerLimit)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    valueToCheck,
                    string.Format(
                        "Value is less than or equal to {0}",
                        lowerLimit));
            }
        }

        /// <summary>
        /// Throws ArgumentException if the value is equal to the undesired value. 
        /// </summary>
        /// <typeparam name="TValue">The type of value to be validated.</typeparam>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <param name="undesiredValue">The value that valueToCheck should not equal.</param>
        /// <param name="valueToCheck">The value of the parameter being validated.</param>
        public static void IsNotEqual<TValue>(
            string parameterName,
            TValue valueToCheck,
            TValue undesiredValue)
        {
            if (EqualityComparer<TValue>.Default.Equals(valueToCheck, undesiredValue))
            {
                throw new ArgumentException(
                    string.Format(
                        "The given value '{0}' should not equal '{1}'",
                        valueToCheck,
                        undesiredValue),
                    parameterName);
            }
        }

        /// <summary>
        /// Throws ArgumentException if the value is null, an empty string,
        /// or a string containing only whitespace.
        /// </summary>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <param name="valueToCheck">The value of the parameter being validated.</param>
        public static void IsNotNullOrEmptyString(string parameterName, string valueToCheck)
        {
            if (string.IsNullOrWhiteSpace(valueToCheck))
            {
                throw new ArgumentException(
                    "Parameter contains a null, empty, or whitespace string.",
                    parameterName);
            }
        }
    }
}
