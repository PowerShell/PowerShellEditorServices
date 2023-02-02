Get-ChildItem ./file1.ps1
$myScriptVar = 123

class BaseClass {

}

class SuperClass : BaseClass {
    SuperClass([string]$name) {

    }

    SuperClass() { }

    [string]$SomePropWithDefault = 'this is a default value'

    [int]$SomeProp

    [string]MyClassMethod([string]$param1, $param2, [int]$param3) {
        $this.SomePropWithDefault = 'something happend'
        return 'finished'
    }

    [string]
    MyClassMethod([MyEnum]$param1) {
        return 'hello world'
    }
    [string]MyClassMethod() {
        return 'hello world'
    }
}

New-Object SuperClass
$o = [SuperClass]::new()
$o.SomeProp
$o.MyClassMethod()


enum MyEnum {
    First
    Second
    Third
}

[MyEnum]::First
'First' -is [MyEnum]
