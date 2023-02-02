$Global:GlobalVar = 0
$UnqualifiedScriptVar = 1
$Script:ScriptVar2 = 2

"`$Script:ScriptVar2 is $Script:ScriptVar2"

function AFunction {}

filter AFilter {$_}

function AnAdvancedFunction {
    begin {
        $LocalVar = 'LocalVar'
        function ANestedFunction() {
            $nestedVar = 42
            "`$nestedVar is $nestedVar"
        }
    }
    process {}
    end {}
}

workflow AWorkflow {}

class AClass {
    [string]$AProperty

    AClass([string]$AParameter) {

    }

    [void]AMethod([string]$param1, [int]$param2, $param3) {

    }
}

enum AEnum {
    AValue = 0
}

AFunction
1..3 | AFilter
AnAdvancedFunction
