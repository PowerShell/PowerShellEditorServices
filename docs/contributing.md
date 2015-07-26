# Contribution Guidelines

We welcome many kinds of community contributions to this project!  Whether it's a feature implementation, 
bug fix, or a good idea, please create an issue so that we can discuss it.  It is not necessary to create an 
issue before sending a pull request but it may speed up the process if we can discuss your idea before 
you start implementing it.

Because this project exposes a couple different public APIs, we must be very mindful of any potential breaking 
changes.  Some contributions may not be accepted if they risk introducing breaking changes or if they 
don't match the goals of the project.  The core maintainer team has the right of final approval over
any contribution to this project.  However, we are very happy to hear community feedback on any decision 
so that we can ensure we are solving the right problems in the right way.  Check out our [governance]
(governance.md) page to learn more about how we run this project.

## Ways to Contribute

- File a bug or feature request as an [issue](https://github.com/PowerShell/PowerShellEditorServices/issues)
- Comment on existing issues to give your feedback on how they should be fixed/implemented
- Contribute a bug fix or feature implementation by submitting a pull request
- Contribute more unit tests for feature areas that lack good coverage
- Review the pull requests that others submit to ensure they follow [established guidelines]
  (#pull-request-guidelines)
- Help others gets started with the project by contributing documentation or hanging out
  in the Slack chatroom [**TODO: Need to set this up!**]

## Code Contribution Guidelines

Here's a high level list of guidelines to follow to ensure your code contribution is accepted:

- Make sure your change aligns with project goals
- Follow established guidelines for coding style and design
- Follow established guidelines for commit hygiene
- Write unit tests to validate new features and bug fixes
- Ensure that the 'Release' build and unit tests pass locally
- Ensure that the AppVeyor build passes for your change
- Respond to all review feedback and final commit cleanup

### Practice Good Commit Hygiene

First of all, make sure you are practicing [good commit hygiene](http://blog.ericbmerritt.com/2011/09/21/commit-hygiene-and-git.html)
so that your commits provide a good history of the changes you are making.  To be more specific:

- **Write good commit messages**

  Commit messages should be clearly written so that a person can look at the commit log and understand
  how and why a given change was made.  Here is a great model commit message taken from a [blog post
  by Tim Pope](http://tbaggery.com/2008/04/19/a-note-about-git-commit-messages.html):
  
      Capitalized, short (50 chars or less) summary
      
      More detailed explanatory text, if necessary.  Wrap it to about 72
      characters or so.  In some contexts, the first line is treated as the
      subject of an email and the rest of the text as the body.  The blank
      line separating the summary from the body is critical (unless you omit
      the body entirely); tools like rebase can get confused if you run the
      two together.
      
      Write your commit message in the imperative: "Fix bug" and not "Fixed bug"
      or "Fixes bug."  This convention matches up with commit messages generated
      by commands like git merge and git revert.
      
      Further paragraphs come after blank lines.
      
      - Bullet points are okay, too
      
      - Typically a hyphen or asterisk is used for the bullet, followed by a
        single space, with blank lines in between, but conventions vary here
      
      - Use a hanging indent
  
  A change that fixes a known bug with an issue filed should use the proper syntax so that the [issue
  is automatically closed](https://help.github.com/articles/closing-issues-via-commit-messages/) once 
  your change is merged.  Here's an example of what such a commit message should look like:
  
      Fix #3: Catch NullReferenceException from DoThing
      
      This change adds a try/catch block to catch a NullReferenceException that
      gets thrown by DoThing [...]

- **Squash your commits**

  If you are introducing a new feature but have implemented it over multiple commits, 
  please [squash those commits](http://gitready.com/advanced/2009/02/10/squashing-commits-with-rebase.html) 
  into a single commit that contains all the changes in one place.  This especially applies to any "oops"
  commits where a file is forgotten or a typo is being fixed.  Following this approach makes it a lot easier 
  to pull those changes to other branches or roll back the change if necessary.
  
- **Keep individual commits for larger changes**

  You can certainly maintain individual commits for different phases of a big change.  For example, if
  you want to reorganize some files before adding new functionality, have your first commit contain all
  of the file move changes and then the following commit can have all of the feature additions.  We
  highly recommend this approach so that larger commits don't turn into a jumbled mess.

### Add Unit Tests for New Code

If you're adding a new feature to the project, please make sure to include adequate [xUnit](http://xunit.github.io/)
tests with your change.  In this project, we have chosen write out unit tests in a way that uses the
actual PowerShell environment rather than extensive interface mocking.  This allows us to be sure that
our features will work in practice.  

We do both component-level and scenario-level testing depending on what code is being tested.  We don't 
expect contributors to test every possible edge case.  Testing mainline scenarios and the most common 
failure scenarios is often good enough.

We are very happy to accept unit test contributions for any feature areas that are more error-prone than
others.  Also, if you find that a feature fails for you in a specific case, please feel free to file an issue
that includes a unit test which reproduces the problem.  This will allow us to quickly implement a fix
that resolves the problem.

### Build 'Release' Before Submitting

Before you send out your pull request, make sure that you have run a Release configuration build of the
project and that all new and existing tests are passing.  The Release configuration build ensures that
all public API interfaces have been documented correctly otherwise it throws an error.  We have turned
on this check so that our project will always have good generated documentation.

### Follow the Pull Request Process

- **Create your pull request**

  Use the [typical process](https://help.github.com/articles/using-pull-requests/) to send a pull request 
  from your fork of the project.  In your pull request message, please give a high-level summary of the
  changes that you have made so that reviewers understand the intent of the changes.  You should receive
  initial comments within a day or two, but please feel free to ping if things are taking longer than
  expected.

- **The build and unit tests must run green**

  When you submit your pull request, our automated build system on AppVeyor will attempt to run a
  Release build of your changes and then run all unit tests against the build.  If you notice that
  any of your unit tests have failed, please fix them by creating a new commit and then pushing it
  to your branch.  If you see that some unrelated test has failed, try re-running the build for your
  pull request.  If you continue to see issues, write a comment on the pull request and we will
  look into it.

- **Respond to code review feedback**

  If the reviewers ask you to make changes, make them as a new commit to your branch and push them so
  that they are made available for a final review pass.  Do not rebase the fixes just yet so that the
  commit hash changes don't upset GitHub's pull request UI.  
  
- **If necessary, do a final rebase**

  Once your final changes have been accepted, we may ask you to do a final rebase to have your commits
  so that they follow our commit guidelines.  If specific guidance is given, please follow it when
  rebasing your commits.  Once you do your final push and we see the AppVeyor build pass, we will
  merge your changes!
  
