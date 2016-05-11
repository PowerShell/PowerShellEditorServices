# Introduction

> NOTE: The user guide is currently under development and may be missing
> important information.  If you feel that a particular area is missing or
> poorly explained, please feel free to file an issue at our [GitHub site](https://github.com/PowerShell/PowerShellEditorServices/issues)

PowerShell Editor Services is a tool that provides useful services to code
editors that need a great PowerShell editing experience.

## The .NET API

The .NET API provides the complete set of services which can be used in
code editors or any other type of application.  The easiest way to get
started with it is to add the [Microsoft.PowerShell.EditorServices](https://www.nuget.org/packages/Microsoft.PowerShell.EditorServices/)
NuGet package to your C# project.

If you're a developer that would like to use PowerShell Editor Services in
a .NET application, read the page titled [Using the .NET API](using_the_dotnet_api.md)
to learn more.

## The Host Process

The host process provides a JSON-based API wrapper around the .NET APIs so
that editors written in non-.NET languages can make use of its capabilities.
In the future the host process will allow the use of network-based channels
to enable all of the APIs to be accessed remotely.

If you're a developer that would like to integrate PowerShell Editor Services
into your favorite editor, read the page titled [Using the Host Process](using_the_host_process.md)
to learn more.

## Writing Extensions in PowerShell

If you're using an editor that leverages PowerShell Editor Services to provide
PowerShell editing capabilities, you may be able to extend its behavior using
our PowerShell-based editor extension API.  Read the page titled [Extending the
Host Editor](extensions.md) to learn more.