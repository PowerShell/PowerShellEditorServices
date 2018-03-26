Describe "A dummy test" {
    Context "When a pester file is given" {
        It "Should return it symbols" {

        }

        It "Should return context symbols" {

        }

        It "Should return describe symbols" {

        }
    }
}

Describe -Tags "Tag1", "Tag2" "Another dummy test" {
    It "Works as expected"
}

Describe "A third test" -Tag "Tag3" {
    Context "Given pester tags" {
        It "Should still detect Pester symbols" {
            $true | Should -BeExactly $true
        }
    }
}
