# Not same
$var = 10
0..10 | Select-Object @{n='SomeProperty';e={ $var = 30 * $_; $var }}
