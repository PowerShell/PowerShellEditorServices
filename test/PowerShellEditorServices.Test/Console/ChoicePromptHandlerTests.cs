//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class ChoicePromptHandlerTests
    {
        private readonly ChoiceDetails[] Choices =
            new ChoiceDetails[]
            {
                new ChoiceDetails("&Apple", ""),
                new ChoiceDetails("Ba&nana", ""),
                new ChoiceDetails("&Orange", "")
            };

        private const int DefaultChoice = 1;

        [Fact]
        public void ChoicePromptReturnsCorrectIdForChoice()
        {
            TestChoicePromptHandler choicePromptHandler = new TestChoicePromptHandler();
            Task<int> promptTask =
                choicePromptHandler.PromptForChoice(
                    "Test prompt",
                    "Message is irrelevant",
                    Choices,
                    DefaultChoice,
                    CancellationToken.None);

            choicePromptHandler.HandleResponse("apple");

            // Wait briefly for the prompt task to complete
            promptTask.Wait(1000);

            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Assert.Equal(0, promptTask.Result);
            Assert.Equal(1, choicePromptHandler.TimesPrompted);
        }

        [Fact]
        public void ChoicePromptReturnsCorrectIdForHotKey()
        {
            TestChoicePromptHandler choicePromptHandler = new TestChoicePromptHandler();
            Task<int> promptTask =
                choicePromptHandler.PromptForChoice(
                    "Test prompt",
                    "Message is irrelevant",
                    Choices,
                    DefaultChoice,
                    CancellationToken.None);

            // Try adding whitespace to ensure it works
            choicePromptHandler.HandleResponse(" N  ");

            // Wait briefly for the prompt task to complete
            promptTask.Wait(1000);

            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Assert.Equal(1, promptTask.Result);
            Assert.Equal(1, choicePromptHandler.TimesPrompted);
        }

        [Fact]
        public void ChoicePromptRepromptsOnInvalidInput()
        {
            TestChoicePromptHandler choicePromptHandler = 
                new TestChoicePromptHandler();

            Task<int> promptTask =
                choicePromptHandler.PromptForChoice(
                    "Test prompt",
                    "Message is irrelevant",
                    Choices,
                    DefaultChoice,
                    CancellationToken.None);

            // Choice is invalid, should reprompt
            choicePromptHandler.HandleResponse("INVALID");

            Assert.Equal(TaskStatus.WaitingForActivation, promptTask.Status);
            Assert.Equal(2, choicePromptHandler.TimesPrompted);
        }
    }

    internal class TestChoicePromptHandler : ChoicePromptHandler
    {
        public int TimesPrompted { get; private set; }

        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            // No action needed, just count the prompts.
            this.TimesPrompted++;
        }
    }
}

