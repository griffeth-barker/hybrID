$projectRoot = Split-Path -Path $PSScriptRoot -Parent
$scriptRoot = Join-Path -Path $projectRoot -ChildPath "hybrID"

Describe "PowerShell source parsing" {
    $scriptFiles = Get-ChildItem -Path $scriptRoot -Filter "*.ps1" -Recurse | Sort-Object FullName

    It "finds PowerShell files to validate" {
        $scriptFiles.Count | Should BeGreaterThan 0
    }

    foreach ($file in $scriptFiles) {
        It "parses $($file.Name) without syntax errors" {
            $tokens = $null
            $errors = $null
            [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
            $errors.Count | Should Be 0
        }
    }
}
