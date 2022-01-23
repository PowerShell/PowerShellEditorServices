$SCRIPT:simpleArray = @(
    1
    2
    'red'
    'blue'
)
$SCRIPT:nestedArray = @(
    1
    2
    @(
        'red'
        'blue'
    )
)
function __BreakDebuggerEnumerableShowsSummaryOnly{}; __BreakDebuggerEnumerableShowsSummaryOnly

#This is a dummy function that the test will use to stop and evaluate the debug environment
