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

Configuration AConfiguration {
    Node "TEST-PC" {}
}

AFunction
1..3 | AFilter
AnAdvancedFunction