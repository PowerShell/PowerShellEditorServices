# PowerShell Editor Services Extensibility Model

PowerShell Editor Services exposes a common extensibility model which allows
a user to write extension code in PowerShell that works across any editor that
uses PowerShell Editor Services.

## Using Extensions

**TODO**

- Enable-EditorExtension -Name "SomeExtension.CustomAnalyzer"
- Disable-EditorExtension -Name "SomeExtension.CustomAnalyzer"

## Writing Extensions

Here are some examples of writing editor extensions:

### Command Extensions

#### Executing a cmdlet or function

```powershell
function MyExtensionFunction {
    Write-Output "My extension function was invoked!"
}

Register-EditorExtension `
    -Command
    -Name "MyExt.MyExtensionFunction" `
    -DisplayName "My extension function" `
    -Function MyExtensionFunction
```

#### Executing a script block

```powershell
Register-EditorExtension `
    -Command
    -Name "MyExt.MyExtensionScriptBlock" `
    -DisplayName "My extension script block" `
    -ScriptBlock { Write-Output "My extension script block was invoked!" }
```

#### Additional Parameters

##### ExecuteInSession [switch]

Causes the command to be executed in the user's current session.  By default,
commands are executed in a global session that isn't affected by script
execution.  Adding this parameter will cause the command to be executed in the
context of the user's session.

### Analyzer Extensions

```powershell
function Invoke-MyAnalyzer {
    param(
        $FilePath,
        $Ast,
        $StartLine,
        $StartColumn,
        $EndLine,
        $EndColumn
    )
}

Register-EditorExtension `
    -Analyzer
    -Name "MyExt.MyAnalyzer" `
    -DisplayName "My analyzer extension" `
    -Function Invoke-MyAnalyzer
```

#### Additional Parameters

##### DelayInterval [int]

Specifies the interval after which this analyzer will be run when the
user finishes typing in the script editor.

### Formatter Extensions

```powershell
function Invoke-MyFormatter {
    param(
        $FilePath,
        $ScriptText,
        $StartLine,
        $StartColumn,
        $EndLine,
        $EndColumn
    )
}

Register-EditorExtension `
    -Formatter
    -Name "MyExt.MyFormatter" `
    -DisplayName "My formatter extension" `
    -Function Invoke-MyFormatter
```

#### Additional Parameters

##### SupportsSelections [switch]

Indicates that this formatter extension can format selections in a larger
file rather than formatting the entire file.  If this parameter is not
specified then the entire file will be sent to the extension for every
call.

## Examples

