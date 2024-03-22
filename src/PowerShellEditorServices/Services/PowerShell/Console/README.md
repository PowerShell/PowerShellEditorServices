# How We Handle `Console.ReadKey()` Being Uncancellable

The C# API `Console.ReadKey()` is synchronous and uncancellable. This is problematic in a
asynchronous application that needs to cancel it.

## The Problem

We host a multi-threaded application. One thread is always servicing the REPL, which runs
PSReadLine, which loops on a `ReadKey` call. Other threads handle various PowerShell
requests via LSP, some of which necessitate interrupting PSReadLine and taking over the
foreground (such as: start debugging, run code). While we have a smart task queue which
correctly handles the cancellation of tasks, including the delegate calling PSReadLine, we
cannot cancel `ReadKey` because as a synchronous .NET API, it is uncancellable.

So, no matter what, _at least one key must be consumed_ before PSReadLine's call to
`ReadKey` is actually "canceled" (in this case, returned). This leads to bugs like [#3881]
since the executed code is now using PowerShell's own prompting to get input from the
user. Until our consumer (the `ReadKey` call) returns, the code behind the scenes of
`$Host.UI.PromptForChoice()` won't get input. The actual fix would be for our `ReadKey`
call to return without having received input after we canceled it (but we can't, so it
doesn't).

[#3881]: https://github.com/PowerShell/vscode-powershell/issues/3881

A non-exhaustive list of known issues likely caused by this:

- [#3881](https://github.com/PowerShell/vscode-powershell/issues/3881)
- [#3756](https://github.com/PowerShell/vscode-powershell/issues/3756)
- [#2741](https://github.com/PowerShell/vscode-powershell/issues/2741)
- [#3876](https://github.com/PowerShell/vscode-powershell/issues/3876)
- [#2832](https://github.com/PowerShell/vscode-powershell/issues/2832)
- [#2169](https://github.com/PowerShell/vscode-powershell/issues/2169)
- [#1753](https://github.com/PowerShell/vscode-powershell/issues/1753)
- [#3225](https://github.com/PowerShell/vscode-powershell/issues/3225)

For what it's worth, Tyler and have had conversations with the .NET team about making
`ReadKey` cancelable. [#801] is an ancient GitHub issue with .NET, and we have had
internal conversations.

[#801]: https://github.com/dotnet/runtime/issues/801

## Previous Workaround(s)

A previous workaround for this was to reinvent PowerShell's prompt handlers so they use
our `ReadKey` call, see [#1583]. This is awful! It duplicates a lot of code when
everything works so almost right without any of this. Except a key needs to be entered to
"cancel" `ReadKey`.

[#1583]: https://github.com/PowerShell/PowerShellEditorServices/issues/1583

Now when I say "our `ReadKey`" call that's because we _already_ had some other workaround
in place for this. Once upon a time (in the days of PowerShell 6 with older .NET Core
versions), on macOS and Linux, if a thread was sitting in a `Console.ReadKey` loop, other
`System.Console` APIs could not safely be called. For instance, `Console.CursorTop` is
readily queried other events (such as the window resizing) and would deadlock, see
[#1748]. So on these OS's we actually didn't use `Console.ReadKey` at all, but implemented
a fake "`ReadKey`" that sits in a loop polling `Console.KeyAvailable`, see the
[`ConsolePal.Unix`] implementation.

[#1748]: https://github.com/PowerShell/PowerShellEditorServices/pull/1748#issuecomment-1079055612
[`ConsolePal.Unix`]: https://github.com/dotnet/runtime/blob/3ff8d262e504d03977edeb67da2b83d01c9ed2db/src/libraries/System.Console/src/System/ConsolePal.Unix.cs#L121-L138

This workaround led to other terrible behaviors, like the "typewriter" effect when pasting
into our terminal, see [#3756]. Note that this issue only occurred on macOS and Linux,
because on Windows we were still calling `Console.ReadKey`, but with a buffer to make it
sort of cancellable, see [`ConsolePal.Windows`]. This is also the reason that [#3881] is
Windows-specific. This makes pasting no macOS and Linux almost unusable, it takes minutes
if you're pasting in a script to run.

[#3756]: https://github.com/PowerShell/vscode-powershell/issues/3756
[`ConsolePal.Windows`]: https://github.com/dotnet/runtime/blob/3ff8d262e504d03977edeb67da2b83d01c9ed2db/src/libraries/System.Console/src/System/ConsolePal.Windows.cs#L307-L400

Another issue that is probably caused by these alternative "`ReadKey`" implementations is
[#2741] where pasting totally fails. It seems like this has appeared before, and was
previously fixed in [#2291].

[#2741]: https://github.com/PowerShell/vscode-powershell/issues/2741
[#2291]: https://github.com/PowerShell/vscode-powershell/issues/2291

As an aside, but important note: these custom "`ReadKey`" implementations are the reason
we have a private [contract] with PSReadLine, where we literally override the `ReadKey`
method in that library when we load it, because PSReadLine is what is actually looping
over `ReadKey`.

[contract]: https://github.com/PowerShell/PSReadLine/blob/dc38b451bee4bdf07f7200026be02516807faa09/PSReadLine/ConsoleLib.cs#L12-L17

## Explored But Inviable Workarounds

Back to [#3881] ("PowerShell prompts ignore the first input character"): one workaround
could be to use the macOS/Linux `KeyAvailable`-based `ReadKey` alternative. But this
should be avoided for several reasons (typewriter effect, battery drain, kind of just
plain awful). It could be better if we improved the polling logic to slow way down after
no input and speed up to instantaneous with input (like when pasting), but it would still
be just a workaround.

An option I explored was to send a known ASCII control character every time the integrated
console _received focus_ and have our `ReadKey` implementation ignore it (but return,
since it received the key its stuck waiting for). This seemed like an ingenious solution,
but unfortunately Visual Studio Code does not have an API for "on terminal focus" and it
won't be getting one any time soon (I explored all the options in the [window] API, and
confirmed with Tyler Leonhardt and Johannes Rieken, two VS Code developers). Theoretically
we could have instead sent the character when our `RunCode` command is called but that
only solves the problem some of the time. However, through this experiment I discovered
that there is now an API to send arbitrary text over `stdin` to our extension-owned
terminal, which is going to useful.

[window]: https://code.visualstudio.com/api/references/vscode-api#window

Another option explored was a custom `CancelReadKey` function that manually wrote a
character to the PSES's own process's `stdin` in order to get `ReadKey` to return. While I
was able to write the character (after using a P/Invoke to libc's `write()` function,
because C#'s own `stdin` stream is opened, aptly, in read-only mode), it was not
sufficient. For some reason, although the data was sent, `ReadKey` ignored it. Maybe
`stdin` is redirected, or something else is going on, unfortunately I'm not sure. However,
this exploration gave me the idea to hook up an LSP notification and have Code send a
non-printing character when `CancelReadKey` is called, since Code is already hooked up to
PSES's `stdin` and now has an API to directly write to it. More on this later.

Another workaround for all these issues is to write our own `ReadKey`, in native code
instead of C# (so as to avoid all the issues with the `System.Console` APIs).
Theoretically, we could write a cross-platform Rust app that simply reads `stdin`,
translates input to `KeyInfo` structures, and passes that over a named pipe back PSES,
which consumes that input in a "`ReadKey`" delegate using a channel pattern queue. This
delegate would spawn the subprocess and hook the parent process's `stdin` to its own
`stdin` (just pipe it across), and when the delegate is canceled, it kills the child
process and unhooks the `stdin` redirect, essentially making this native app a
"cancellable `ReadKey`." We did not end up trying this approach due to the cost involved
in prototype it, as we would essentially be writing our own native replacement for:
[`Console`]. We'd also need to deal with the fact that `Console` doesn't like `stdin`
being [redirected], as PSReadLine indicates is already an issue.

[`Console`]: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Console/src/System/Console.cs
[redirected]: https://github.com/PowerShell/PSReadLine/blob/f46f15d2d634e2060bc0eabe4c81fc13a5a64a3a/PSReadLine/ReadLine.cs#L343-L356

## Current Working Solution

After trying a few different workarounds, something finally clicked and I combined several
of the ideas. I realized that we already have an external process writing to PSES's
`stdin`, and that's VS Code itself. Moreover, it now has a `SendText` API for the object
representing the extension owned terminal (which is hosting PSES). So in [#1751], I wired
up the cancellation token in our "safe, cancellable" `ReadKey` to send an LSP notification
to the client called `sendKeyPress`, which on the client side simply uses that API to send
a character (we eventually chose `p` because it's easy to see if something has gone wrong)
to the terminal, _just as if the user had pressed a key_. This causes `Console.ReadKey` to
return, since it received the input it was waiting on, and because we know that we
requested a cancellation (through the token), we can ignore that input and move forward
just as if the .NET API itself were canceled. Several things came together to make this
solution viable:

- The pipeline execution threading rewrite meant that we don't have race conditions around
  processing input
- VS Code added an API for us to write directly to our process's `stdin`.
- We dropped support for PowerShell 6 meaning that the .NET `System.Console` APIs on macOS
  and Linux no longer deadlock each other.

This workaround resolved many issues, and the same workaround could be able applied to
other LSP clients that host a terminal (like Vim). Moreover, we deleted over a thousand
lines of code and added less than eighty! We did have to bake this workaround for a while,
and it required PR [#3274] to PSReadLine, as well as a later race condition fix in PR
[#3294]. I still hope that one day we can have an asynchronous `Console.ReadKey` API that
accepts a cancellation token, and so does not require this fake input to return and free
up our thread.

[#1751]: https://github.com/PowerShell/PowerShellEditorServices/pull/1751
[#3274]: https://github.com/PowerShell/PSReadLine/pull/3274
[#3294]: https://github.com/PowerShell/PSReadLine/pull/3294
