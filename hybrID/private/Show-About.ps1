function Show-About {
    $aboutPath = Join-Path $global:uiDir "about.xaml"
    [xml]$aboutXaml = Get-Content -Path $aboutPath -Raw
    $aboutReader = (New-Object System.Xml.XmlNodeReader $aboutXaml)
    $aboutWindow = [Windows.Markup.XamlReader]::Load($aboutReader)

    # Apply the UI theme
    Apply-Theme -TargetWindow $aboutWindow 

    # Load and apply the app icon
    if ($global:MainWindow.Icon) {
        $aboutWindow.Icon = $global:MainWindow.Icon
    }

    $btnClose = $aboutWindow.FindName("btnClose")
    $lnkGitHub = $aboutWindow.FindName("lnkGitHub")

    # Inherit hyperlink color from the global theme engine
    $lnkGitHub.Foreground = $global:LinkBrush

    # Bind Events
    $btnClose.Add_Click({ $aboutWindow.Close() })

    $lnkGitHub.Add_RequestNavigate({
        if ($_.Uri) { 
            # Open the GitHub link in the user's default web browser
            Start-Process $_.Uri.AbsoluteUri
            $_.Handled = $true
        }
    })

    $aboutWindow.ShowDialog() | Out-Null
}