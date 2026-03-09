$projectRoot = Split-Path -Path $PSScriptRoot -Parent
$privateDir = Join-Path -Path $projectRoot -ChildPath "hybrID\private"
$configPath = Join-Path -Path $projectRoot -ChildPath "hybrID\config\config.json"
$uiDir = Join-Path -Path $projectRoot -ChildPath "hybrID\ui"

Describe "Project conventions" {
    It "has valid config JSON" {
        { Get-Content -Path $configPath -Raw | ConvertFrom-Json | Out-Null } | Should Not Throw
    }

    It "has valid XAML documents" {
        $xamlFiles = Get-ChildItem -Path $uiDir -Filter "*.xaml" -File
        $xamlFiles.Count | Should BeGreaterThan 0

        foreach ($xamlFile in $xamlFiles) {
            { [xml](Get-Content -Path $xamlFile.FullName -Raw) | Out-Null } | Should Not Throw
        }
    }

    $privateScripts = Get-ChildItem -Path $privateDir -Filter "*.ps1" -File | Sort-Object Name
    foreach ($script in $privateScripts) {
        $expectedFunction = [System.IO.Path]::GetFileNameWithoutExtension($script.Name)
        $escapedFunctionName = [regex]::Escape($expectedFunction)

        It "private script $($script.Name) defines function $expectedFunction" {
            $content = Get-Content -Path $script.FullName -Raw
            $content | Should Match "(?im)^\s*function\s+$escapedFunctionName\b"
        }
    }
}
