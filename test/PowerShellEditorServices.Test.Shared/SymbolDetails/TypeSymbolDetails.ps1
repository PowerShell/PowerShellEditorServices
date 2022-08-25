class SuperClass {
    SuperClass([string]$name) {

    }

    [string]$SomePropWithDefault = 'this is a default value'

    [int]$SomeProp

    [string]MyClassMethod([string]$param1, $param2, [int]$param3) {
        $this.SomePropWithDefault = 'something happend'
        return 'finished'
    }
}

New-Object SuperClass
$o = [SuperClass]::new()

enum MyEnum {
    First
    Second
    Third
}
