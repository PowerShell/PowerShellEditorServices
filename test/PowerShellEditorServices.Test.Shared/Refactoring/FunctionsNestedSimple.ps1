function Outer {
    write-host "Hello World"

    function Inner {
        write-host "Hello World"
    }
    Inner

}

SingleFunction
