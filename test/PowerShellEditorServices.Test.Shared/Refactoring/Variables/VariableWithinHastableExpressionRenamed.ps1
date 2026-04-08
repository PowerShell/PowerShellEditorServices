# Not same
$var = 10
0..10 | Select-Object @{n='SomeProperty';e={ $Renamed = 30 * $_; $Renamed }}
