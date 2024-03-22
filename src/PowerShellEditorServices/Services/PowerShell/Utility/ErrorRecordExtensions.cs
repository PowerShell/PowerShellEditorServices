// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class ErrorRecordExtensions
    {
        private static readonly Action<PSObject> s_setWriteStreamProperty;

        [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "cctor needed for version specific initialization")]
        static ErrorRecordExtensions()
        {
            if (VersionUtils.IsPS7OrGreater)
            {
                // Used to write ErrorRecords to the Error stream. Using Public and NonPublic because the plan is to make this property
                // public in 7.0.1
                PropertyInfo writeStreamProperty = typeof(PSObject).GetProperty("WriteStream", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Type writeStreamType = typeof(PSObject).Assembly.GetType("System.Management.Automation.WriteStreamType");
                object errorStreamType = Enum.Parse(writeStreamType, "Error");

                ParameterExpression errorObjectParameter = Expression.Parameter(typeof(PSObject));

                // Generates a call like:
                //  $errorPSObject.WriteStream = [System.Management.Automation.WriteStreamType]::Error
                // So that error record PSObjects will be rendered in the console properly
                // See https://github.com/PowerShell/PowerShell/blob/946341b2ebe6a61f081f4c9143668dc7be1f9119/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHost.cs#L2088-L2091
                s_setWriteStreamProperty = Expression.Lambda<Action<PSObject>>(
                    Expression.Call(
                        errorObjectParameter,
                        writeStreamProperty.GetSetMethod(nonPublic: true),
                        Expression.Constant(errorStreamType)),
                    errorObjectParameter)
                    .Compile();
            }
        }

        public static PSObject AsPSObject(this ErrorRecord errorRecord)
        {
            PSObject errorObject = PSObject.AsPSObject(errorRecord);

            // Used to write ErrorRecords to the Error stream so they are rendered in the console correctly.
            if (s_setWriteStreamProperty != null)
            {
                s_setWriteStreamProperty(errorObject);
            }
            else
            {
                PSNoteProperty note = new("writeErrorStream", true);
                errorObject.Properties.Add(note);
            }

            return errorObject;
        }
    }
}
