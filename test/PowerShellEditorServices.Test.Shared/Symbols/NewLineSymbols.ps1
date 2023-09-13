function
returnTrue {
    $true
}

class
NewLineClass {
    NewLineClass() {

    }

    static
    hidden
    [string]
    $SomePropWithDefault = 'some value'

    static
    hidden
    [string]
    MyClassMethod([MyNewLineEnum]$param1) {
        return 'hello world $param1'
    }
}

enum
MyNewLineEnum {
    First
}
