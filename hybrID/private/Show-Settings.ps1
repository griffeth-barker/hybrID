function Show-Settings {
    function Set-WindowIcon ($TargetWindow) {
        $iconPath = Join-Path -Path $global:assetsDir -ChildPath "hybrID-icon-nobg-x128.ico"
        if (Test-Path $iconPath) {
            $iconUri = [System.Uri]::new($iconPath, [System.UriKind]::Absolute)
            $TargetWindow.Icon = [System.Windows.Media.Imaging.BitmapFrame]::Create($iconUri)
        }
    }

    # Load Settings Window XAML
    $settingsPath = Join-Path $global:uiDir "settings.xaml"
    [xml]$settingsXaml = Get-Content -Path $settingsPath -Raw
    $settingsReader = (New-Object System.Xml.XmlNodeReader $settingsXaml)
    $settingsWindow = [Windows.Markup.XamlReader]::Load($settingsReader)

    Apply-Theme -TargetWindow $settingsWindow
    
    # Apply the icon safely
    Set-WindowIcon -TargetWindow $settingsWindow

    # Map UI Controls
    $cfgFqdn = $settingsWindow.FindName("cfgFqdn")
    $cmbTheme = $settingsWindow.FindName("cmbTheme")
    $btnApply = $settingsWindow.FindName("btnApply")
    $btnCancel = $settingsWindow.FindName("btnCancel")

    # Populate Current Values
    $cfgFqdn.Text = $global:Config.Settings.OnPremExchangeFqdn
    
    $currentTheme = $global:Config.Settings.Theme
    if ($currentTheme -eq "Light") { $cmbTheme.SelectedIndex = 1 }
    elseif ($currentTheme -eq "Dark") { $cmbTheme.SelectedIndex = 2 }
    else { $cmbTheme.SelectedIndex = 0 }

    # Bind Events
    $btnCancel.Add_Click({ $settingsWindow.Close() })

    $btnApply.Add_Click({
        $global:Config.Settings.OnPremExchangeFqdn = $cfgFqdn.Text.Trim()
        
        $oldTheme = $global:Config.Settings.Theme
        $newTheme = $cmbTheme.SelectedItem.Content
        $themeChanged = ($oldTheme -ne $newTheme)

        $global:Config.Settings.Theme = $newTheme

        $jsonOutput = $global:Config | ConvertTo-Json -Depth 4
        $jsonOutput = $jsonOutput -replace '\\/', '/'
        $jsonOutput | Set-Content -Path $global:configPath

        if ($themeChanged) {
            # Load Restart Alert XAML
            $alertPath = Join-Path $global:uiDir "alert-restart.xaml"
            [xml]$alertXaml = Get-Content -Path $alertPath -Raw
            $alertReader = (New-Object System.Xml.XmlNodeReader $alertXaml)
            $alertWindow = [Windows.Markup.XamlReader]::Load($alertReader)
            
            Apply-Theme -TargetWindow $alertWindow 
            
            # Apply the icon safely inside the event
            Set-WindowIcon -TargetWindow $alertWindow
            
            $btnOk = $alertWindow.FindName("btnOk")
            $btnOk.Add_Click({ $alertWindow.Close() })

            $alertWindow.ShowDialog() | Out-Null
            $settingsWindow.Close()

            if (Test-Path $global:appExecutionPath) {
                $wshell = New-Object -ComObject WScript.Shell
                $command = "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$global:appExecutionPath`""
                $wshell.Run($command, 0, $false) | Out-Null
                
                $global:MainWindow.Close()
            }
        } else {
            # Load Success Alert XAML
            $successPath = Join-Path $global:uiDir "alert-success.xaml"
            [xml]$successXaml = Get-Content -Path $successPath -Raw
            $successReader = (New-Object System.Xml.XmlNodeReader $successXaml)
            $successWindow = [Windows.Markup.XamlReader]::Load($successReader)
            
            Apply-Theme -TargetWindow $successWindow 
            
            # Apply the icon safely inside the event
            Set-WindowIcon -TargetWindow $successWindow
            
            $btnOk = $successWindow.FindName("btnOk")
            $btnOk.Add_Click({ $successWindow.Close() })

            $successWindow.ShowDialog() | Out-Null
            $settingsWindow.Close()
        }
    })

    $settingsWindow.ShowDialog() | Out-Null
}