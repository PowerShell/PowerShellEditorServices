//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
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

        [Trait("Category", "Prompt")]
        [Fact(Skip = "This test fails often and is not designed well...")]
        public void ChoicePromptReturnsCorrectIdForChoice()
        {
            TestChoicePromptHandler choicePromptHandler = new TestChoicePromptHandler();
            Task<int> promptTask =
                choicePromptHandler.PromptForChoiceAsync(
                    "Test prompt",
                    "Message is irrelevant",
                    Choices,
                    DefaultChoice,
                    CancellationToken.None);

            choicePromptHandler.ReturnInputString("apple");

            // Wait briefly for the prompt task to complete
            promptTask.Wait(1000);

            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Assert.Equal(0, promptTask.Result);
            Assert.Equal(1, choicePromptHandler.TimesPrompted);
        }

        [Trait("Category", "Prompt")]
        [Fact(Skip = "Hotkeys are not exposed while VSCode ignores them")]
        public void ChoicePromptReturnsCorrectIdForHotKey()
        {
            TestChoicePromptHandler choicePromptHandler = new TestChoicePromptHandler();
            Task<int> promptTask =
                choicePromptHandler.PromptForChoiceAsync(
                    "Test prompt",
                    "Message is irrelevant",
                    Choices,
                    DefaultChoice,
                    CancellationToken.None);

            // Try adding whitespace to ensure it works
            choicePromptHandler.ReturnInputString(" N  ");

            // Wait briefly for the prompt task to complete
            promptTask.Wait(1000);

            Assert.Equal(TaskStatus.RanToCompletion, promptTask.Status);
            Assert.Equal(1, promptTask.Result);
            Assert.Equal(1, choicePromptHandler.TimesPrompted);
        }

        [Trait("Category", "Prompt")]
        [Fact]
        public async Task ChoicePromptRepromptsOnInvalidInput()
        {
            TestChoicePromptHandler choicePromptHandler =
                new TestChoicePromptHandler();

            Task<int> promptTask =
                choicePromptHandler.PromptForChoiceAsync(
                    "Test prompt",
                    "Message is irrelevant",
                    Choices,
                    DefaultChoice,
                    CancellationToken.None);

            // Choice is invalid, should reprompt
            choicePromptHandler.ReturnInputString("INVALID");

            // Give time for the prompt to reappear.
            await Task.Delay(1000).ConfigureAwait(false);

            Assert.Equal(TaskStatus.WaitingForActivation, promptTask.Status);
            Assert.Equal(2, choicePromptHandler.TimesPrompted);
        }
    }

    internal class TestChoicePromptHandler : ChoicePromptHandler
    {
        private TaskCompletionSource<string> linePromptTask;

        public int TimesPrompted { get; private set; }

        public TestChoicePromptHandler() : base(NullLogger.Instance)
        {
        }

        public void ReturnInputString(string inputString)
        {
            this.linePromptTask.SetResult(inputString);
        }

        protected override Task<string> ReadInputStringAsync(CancellationToken cancellationToken)
        {
            this.linePromptTask = new TaskCompletionSource<string>();
            return this.linePromptTask.Task;
        }

        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            // No action needed, just count the prompts.
            this.TimesPrompted++;
        }
    }
}

