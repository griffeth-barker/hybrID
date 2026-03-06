function Show-About {
    $aboutPath = Join-Path $global:uiDir "about.xaml"
    [xml]$aboutXaml = Get-Content -Path $aboutPath -Raw
    $aboutReader = (New-Object System.Xml.XmlNodeReader $aboutXaml)
    $aboutWindow = [Windows.Markup.XamlReader]::Load($aboutReader)

    # Apply the UI theme
    Apply-Theme -TargetWindow $aboutWindow 

    # Load and apply the app icon
    $iconPath = Join-Path -Path $global:assetsDir -ChildPath "hybrID-icon.ico"
    if (Test-Path $iconPath) {
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
        $bmp.BeginInit()
        $bmp.UriSource = [System.Uri]::new($iconPath, [System.UriKind]::Absolute)
        $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        $bmp.EndInit()
        $aboutWindow.Icon = $bmp
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