//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class InputPromptHandlerTests
    {
        const string NameField = "Name";
        const string NameValue = "John Doe";

        const string AgeField = "Age";
        const int AgeValue = 67;

        const string BooksField = "Books";
        static readonly ArrayList BookItems = new ArrayList(new string[] { "Neuromancer", "Tao Te Ching" });

        private readonly FieldDetails[] Fields =
            new FieldDetails[]
            {
                new FieldDetails(NameField, "Name", typeof(string), false, "Default Name"),
                new FieldDetails(AgeField, "Age", typeof(int), true, 30),
                new FieldDetails(BooksField, "Favorite Books", typeof(IList), false, null)
            };

        [Fact]
        public void InputPromptHandlerReturnsValuesOfCorrectType()
        {
            TestInputPromptHandler inputPromptHandler = new TestInputPromptHandler();
            Task<Dictionary<string, object>> promptTask =
                inputPromptHandler.PromptForInput(
                    "Test Prompt",
                    "Message is irrelevant",
                    Fields,
                    CancellationToken.None);

            Assert.Equal(NameField, inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse(NameValue);

            Assert.Equal(AgeField, inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse(AgeValue.ToString());

            Assert.Equal(BooksField + "[0]", inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse((string)BookItems[0]);
            Assert.Equal(BooksField + "[1]", inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse((string)BookItems[1]);
            Assert.Equal(BooksField + "[2]", inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse("");

            // Make sure the right results are returned
            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Dictionary<string, object> fieldValues = promptTask.Result;
            Assert.Equal(NameValue, fieldValues[NameField]);
            Assert.Equal(AgeValue, fieldValues[AgeField]);
            Assert.Equal(BookItems, fieldValues[BooksField]);
        }

        [Fact]
        public void InputPromptHandlerAcceptsArrayOfNonStringValues()
        {
            TestInputPromptHandler inputPromptHandler = new TestInputPromptHandler();
            Task<Dictionary<string, object>> promptTask =
                inputPromptHandler.PromptForInput(
                    "Test Prompt",
                    "Message is irrelevant",
                    new FieldDetails[]
                    {
                        new FieldDetails("Numbers", "Numbers", typeof(int[]), false, null)
                    },
                    CancellationToken.None);

            Assert.Equal("Numbers[0]", inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse("1");
            inputPromptHandler.HandleResponse("");

            // Make sure the right results are returned
            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Dictionary<string, object> fieldValues = promptTask.Result;
            Assert.Equal(new int[] { 1 }, fieldValues["Numbers"]);
        }

        [Fact]
        public void InputPromptRetriesWhenCannotCastValue()
        {
            TestInputPromptHandler inputPromptHandler = new TestInputPromptHandler();
            Task<Dictionary<string, object>> promptTask =
                inputPromptHandler.PromptForInput(
                    "Test Prompt",
                    "Message is irrelevant",
                    Fields,
                    CancellationToken.None);

            Assert.Equal(NameField, inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse(NameValue);

            // Intentionally give a non-integer string to cause an error
            Assert.Equal(AgeField, inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse(NameValue);
            Assert.NotNull(inputPromptHandler.LastError);

            // Give the right value the next time
            Assert.Equal(AgeField, inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse(AgeValue.ToString());

            Assert.Equal(BooksField + "[0]", inputPromptHandler.LastField.Name);
            inputPromptHandler.HandleResponse("");

            // Make sure the right results are returned
            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Dictionary<string, object> fieldValues = promptTask.Result;
            Assert.Equal(AgeValue, fieldValues[AgeField]);
        }
    }

    internal class TestInputPromptHandler : InputPromptHandler
    {
        public FieldDetails LastField { get; private set; }

        public Exception LastError { get; private set; }

        protected override void ShowPromptMessage(string caption, string message)
        {
        }

        protected override void ShowFieldPrompt(FieldDetails fieldDetails)
        {
            this.LastField = fieldDetails;
        }

        protected override void ShowErrorMessage(Exception e)
        {
            this.LastError = e;
        }
    }
}

