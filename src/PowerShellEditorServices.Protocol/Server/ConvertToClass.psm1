function Test-String {
    param($p)

    $testResult = $false
    if($p -is [System.ValueType] -Or $p -is [string]) {
        $testResult=$p -is [string]
    }

    [PSCustomObject]@{
        Test=$testResult
        DataType = "string"
    }
}

function Test-Date {
    param($p)

    $testResult = $false
    if($p -is [System.ValueType] -Or $p -is [string]) {
        [datetime]$result  = [datetime]::MinValue
        $testResult=[datetime]::TryParse($p, [ref]$result)
    }

    [PSCustomObject]@{
        Test=$testResult
        DataType = "datetime"
    }
}

function Test-Boolean {
    param($p)

    $testResult = $false
    if($p -is [System.ValueType]) {
        [bool]$result=$false
        $testResult = [bool]::TryParse($p, [ref]$result)
    }

    [PSCustomObject]@{
        Test=$testResult
        DataType = "bool"
    }
}

function Test-Number {
    param($p)

    $testResult = $false
    if($p -is [System.ValueType] -Or $p -is [string]) {
        [double]$result  = [double]::MinValue
        $testResult=[double]::TryParse($p, [ref]$result)
    }

    [PSCustomObject]@{
        Test=$testResult
        DataType = "double"
    }
}

function Test-Integer {
    param($p)

    $testResult = $false
    if($p -is [System.ValueType] -Or $p -is [string]) {
        [int]$result  = [int]::MinValue
        $testResult=[int]::TryParse($p, [ref]$result)
    }

    [PSCustomObject]@{
        Test=$testResult
        DataType = "int"
    }
}

function Test-PSCustomObject {
    param($p)

    $testResult=$p -is [System.Management.Automation.PSCustomObject]

    [PSCustomObject]@{
        Test=$testResult
        DataType = "PSCustomObject"
    }
}

function Test-Array {
    param($p)

    $testResult=$p -is [array]

    [PSCustomObject]@{
        Test=$testResult
        DataType = "Array"
    }
}

$tests = [ordered]@{
    TestBoolean        = Get-Command Test-Boolean
    TestInteger        = Get-Command Test-Integer
    TestNumber         = Get-Command Test-Number
    TestDate           = Get-Command Test-Date
    TestString         = Get-Command Test-String
    TestPSCustomObject = Get-Command Test-PSCustomObject
    TestArray          = Get-Command Test-Array
}

function Invoke-AllTests {
    param(
        $target,
        [Switch]$OnlyPassing,
        [Switch]$FirstOne
    )

    $resultCount=0
    $tests.GetEnumerator() | ForEach {

        $result=& $_.Value $target

        $testResult = [PSCustomObject]@{
            Test     = $_.Key
            Target   = $target
            Result   = $result.Test
            DataType = $result.DataType
        }

        if(!$OnlyPassing) {
            $testResult
        } elseif ($result.Test -eq $true) {
            if($FirstOne) {
                if($resultCount -ne 1) {
                    $testResult
                    $resultCount+=1
                }
            } else {
                $testResult
            }
        }
    }
}

function Get-DataType {
    param($record)

    $p=@($record.psobject.properties.name)

    for ($idx = 0; $idx -lt $p.Count; $idx++) {

        $name = $p[$idx]
        $value = $record.$name

        if($value -eq $null) {
            $dataType = "object"
        } else {
            $result=Invoke-AllTests $value -OnlyPassing -FirstOne
            $dataType = $result.DataType
        }

        [PSCustomObject]@{
            Name         = $name
            Value        = $value
            DataType     = $dataType
        }
    }
}

$PowerShellConverter = @'
function NewClass       ([string]$name) {"class $name {"}
function NewProperty    ([string]$DataType, [string]$Name) { return "    [$DataType]`$$Name"}
function NewArray       ([string]$Name) { return "    [$Name[]]`$$Name"}
function NewObjectArray ([string]$Name) { return "    [object[]]`$$Name"}
function EndClass       ($name) {"}`r`n"}
'@

$CSharpConverter = @'
function NewClass       ([string]$name) {"class $name `r`n{"}
function NewProperty    ([string]$DataType, [string]$Name) { return "`t$DataType $Name {get; set;}"}
function NewArray       ([string]$Name) { return "`t$Name[] $Name {get; set;}"}
function NewObjectArray ([string]$Name) { return "`tobject[] $Name {get; set;}"}
function EndClass       ($name) {"}`r`n"}
'@

$classes = @{}
function Get-ClassName ($className) {

    $classes.$className+=1
    if($classes.$className -eq 1) {
        $className
    } else {
        "$className$($classes.$className-1)"
    }
}

function ConvertTo-Class {
    [CmdletBinding()]
    param(
        $Target,
        $ClassName,
        [ValidateSet('PowerShell','CSharp')]
        $CodeGen="PowerShell",
        $Converter
    )

    if(!$Converter) {
        switch ($CodeGen) {
            'PowerShell' {$Converter=$PowerShellConverter}
            'CSharp'     {$Converter=$CSharpConverter}
        }
    }

    #if($Converter) { $Converter | Invoke-Expression }
    $Converter | Invoke-Expression

    if($target -is [string]) {
        try {
            $cvt = $target | ConvertFrom-Json

            if(!$className) {
                $className = Get-ClassName "RootObject"
            }

            ConvertTo-Class $cvt $className -CodeGen $CodeGen
        } catch {

            try {
                $cvt = $target | ConvertFrom-Csv | select -First 1
                if(!$className) {
                    $className = Get-ClassName "RootObject"
                }

                ConvertTo-Class $cvt $className -CodeGen $CodeGen
            } catch {
                throw "bad data"
            }
        }

        return
    }

    $infered = Get-DataType $target

    $otherClasses=@()

    $xport = switch ($infered) {

        {$_.DataType -eq 'Array'} {
            if($_.Value[0] -is [string] -or $_.Value[0] -is [System.ValueType]) {
                
                Write-Verbose "Object Array $($_.name)"                
                NewObjectArray $_.Name
            } else {
                
                Write-Verbose "Array $($_.name)"                
                if($_.Value.Count -eq 0) {
                    NewObjectArray $_.Name                    
                } else {
                    NewArray $_.Name $_.Name
                    $otherClasses+=ConvertTo-Class ($_.Value | select -First 1) (Get-ClassName $_.name) -CodeGen $CodeGen
                }
            }
        }

        {$_.DataType -eq 'PSCustomObject'} {

            NewProperty $_.Name $_.Name
            $otherClasses+=ConvertTo-Class $_.Value (Get-ClassName $_.name) -CodeGen $CodeGen
        }

        default {
            
            Write-Verbose "Property $($_.DataType) $($_.name)"            
            NewProperty $_.DataType ($_.name -replace "/","")
        }
    }

NewClass $ClassName
@"
$($xport -join "`n")
"@
EndClass

$otherClasses
}
