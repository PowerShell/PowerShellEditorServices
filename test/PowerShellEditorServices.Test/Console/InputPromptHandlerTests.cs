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
using System.Security;

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
                new CollectionFieldDetails(BooksField, "Favorite Books", typeof(IList), false, null)
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
            inputPromptHandler.ReturnInputString(NameValue);

            Assert.Equal(AgeField, inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString(AgeValue.ToString());

            Assert.Equal(BooksField + "[0]", inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString((string)BookItems[0]);
            Assert.Equal(BooksField + "[1]", inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString((string)BookItems[1]);
            Assert.Equal(BooksField + "[2]", inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString("");

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
                        new CollectionFieldDetails("Numbers", "Numbers", typeof(int[]), false, null)
                    },
                    CancellationToken.None);

            Assert.Equal("Numbers[0]", inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString("1");
            inputPromptHandler.ReturnInputString("");

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
            inputPromptHandler.ReturnInputString(NameValue);

            // Intentionally give a non-integer string to cause an error
            Assert.Equal(AgeField, inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString(NameValue);
            Assert.NotNull(inputPromptHandler.LastError);

            // Give the right value the next time
            Assert.Equal(AgeField, inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString(AgeValue.ToString());

            Assert.Equal(BooksField + "[0]", inputPromptHandler.LastField.Name);
            inputPromptHandler.ReturnInputString("");

            // Make sure the right results are returned
            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Dictionary<string, object> fieldValues = promptTask.Result;
            Assert.Equal(AgeValue, fieldValues[AgeField]);
        }
    }

    internal class TestInputPromptHandler : InputPromptHandler
    {
        private TaskCompletionSource<string> linePromptTask;
        private TaskCompletionSource<SecureString> securePromptTask;

        public FieldDetails LastField { get; private set; }

        public Exception LastError { get; private set; }

        public void ReturnInputString(string inputString)
        {
            this.linePromptTask.SetResult(inputString);
        }

        public void ReturnSecureString(SecureString secureString)
        {
            this.securePromptTask.SetResult(secureString);
        }

        protected override Task<string> ReadInputString(CancellationToken cancellationToken)
        {
            this.linePromptTask = new TaskCompletionSource<string>();
            return this.linePromptTask.Task;
        }

        protected override Task<SecureString> ReadSecureString(CancellationToken cancellationToken)
        {
            this.securePromptTask = new TaskCompletionSource<SecureString>();
            return this.securePromptTask.Task;
        }

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

