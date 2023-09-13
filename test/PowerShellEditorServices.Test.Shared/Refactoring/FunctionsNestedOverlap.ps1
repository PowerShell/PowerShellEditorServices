
function Inner {
    write-host "I'm the First Inner"
}
function foo {
    function Inner {
        write-host "Shouldnt be called or renamed at all."
    }
}
function Inner {
    write-host "I'm the First Inner"
}

function Outer {
    write-host "I'm the Outer"
    Inner
    function Inner {
        write-host "I'm in the Inner Inner"
    }
    Inner

}
Outer

function Inner {
    write-host "I'm the outer Inner"
}

Outer
Inner
