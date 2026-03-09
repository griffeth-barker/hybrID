#requires -version 5

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Windows.Forms

# Directory definitions and terminal handling
$publicDir = $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($publicDir)) {
    if ($MyInvocation.MyCommand.Path) {
        # Catch execution if dot-sourced
        $publicDir = Split-Path $MyInvocation.MyCommand.Path
    } else {
        # Fall back to terminal location
        $publicDir = (Get-Location).Path
        
        # If terminal is at the repo root (d:\github\hybrID), adjust path to the public folder
        if (Test-Path (Join-Path $publicDir "hybrID\ui\main.xaml")) {
            $publicDir = Join-Path $publicDir "hybrID\public"
        }
        # If terminal is inside the module root already, adjust to the public folder
        elseif (Test-Path (Join-Path $publicDir "ui\main.xaml")) {
            $publicDir = Join-Path $publicDir "public"
        }
    }
}

$moduleBase = Split-Path -Path $publicDir -Parent
$privateDir = Join-Path -Path $moduleBase -ChildPath "private"

$global:uiDir     = Join-Path -Path $moduleBase -ChildPath "ui"
$global:assetsDir = Join-Path -Path $moduleBase -ChildPath "assets"
$global:configDir = Join-Path -Path $moduleBase -ChildPath "config"
$global:logsDir   = Join-Path -Path $moduleBase -ChildPath "logs"

# Import presentation layer & Icon
$xamlPath = Join-Path -Path $global:uiDir -ChildPath "main.xaml"
$iconPath = Join-Path -Path $global:assetsDir -ChildPath "hybrID-icon-nobg-x128.ico"

if (-not (Test-Path $xamlPath)) {
    [System.Windows.Forms.MessageBox]::Show("Could not find main.xaml in '$global:uiDir'.", "Missing UI File", "OK", "Error")
    Exit
}

[xml]$xamlContent = Get-Content -Path $xamlPath -Raw
$reader = (New-Object System.Xml.XmlNodeReader $xamlContent)
$window = [Windows.Markup.XamlReader]::Load($reader)

# Map the main window and script path globally for the restart logic
$global:MainWindow = $window
$global:appExecutionPath = Join-Path -Path $publicDir -ChildPath "hybrID.ps1"

# Apply the custom icon to the Title Bar and Taskbar
if (-not (Test-Path $iconPath)) {
    [System.Windows.Forms.MessageBox]::Show("Could not find hybrID-icon.ico in '$global:assetsDir'.`n`nPlease ensure the file is in the inner application assets folder, not the repository root assets folder.", "Missing Icon", "OK", "Warning")
} else {
    # Use BitmapFrame with an absolute URI for native Windows icon parsing
    $iconUri = [System.Uri]::new($iconPath, [System.UriKind]::Absolute)
    $window.Icon = [System.Windows.Media.Imaging.BitmapFrame]::Create($iconUri)
}

# Load JSON Configuration
$global:configPath = Join-Path -Path $global:configDir -ChildPath "config.json"
if (-not (Test-Path $global:configPath)) {
    [System.Windows.Forms.MessageBox]::Show("Could not find config.json in '$global:configDir'.", "Missing Configuration", "OK", "Error")
    Exit
}
$global:Config = Get-Content -Path $global:configPath -Raw | ConvertFrom-Json

# Set up UI controls
$btnSettings = $window.FindName("btnSettings")
$btnAbout = $window.FindName("btnAbout")
$txtSearch = $window.FindName("txtSearch")
$btnSearch = $window.FindName("btnSearch")
$statusBar = $window.FindName("statusBar")
$txtStatus = $window.FindName("txtStatus")
$valName = $window.FindName("valName")
$valClass = $window.FindName("valClass")
$txtManageIdentity = $window.FindName("txtManageIdentity")
$txtManageExchange = $window.FindName("txtManageExchange")
$txtSoaState = $window.FindName("txtSoaState")
$btnTransferSoa = $window.FindName("btnTransferSoa")
$btnTransferGroupSoa = $window.FindName("btnTransferGroupSoa")
$valNotes = $window.FindName("valNotes")

$brushConverter = New-Object System.Windows.Media.BrushConverter

# Import private functions
$privateScripts = Get-ChildItem -Path $privateDir -Filter "*.ps1"
foreach ($script in $privateScripts) {
    . $script.FullName
}

# Bindings and init
$btnSettings.Add_Click({ Show-Settings })
$btnAbout.Add_Click({ Show-About })
$btnSearch.Add_Click({ Invoke-Search })
$btnTransferSoa.Add_Click({ Invoke-SoaTransfer })
$btnTransferGroupSoa.Add_Click({ Invoke-GroupSoaTransfer })
$txtSearch.Add_KeyDown({ if ($_.Key -eq 'Return') { Invoke-Search } })

$window.Add_Loaded({
    Assert-GraphConnection
    $txtSearch.Focus() | Out-Null
})

Apply-Theme -TargetWindow $window

$window.ShowDialog() | Out-Null