function Apply-Theme ($TargetWindow) {
    $theme = $global:Config.Settings.Theme
    
    # Check Windows Registry for System Theme
    if ($theme -eq "System" -or [string]::IsNullOrWhiteSpace($theme)) {
        $useLight = (Get-ItemPropertyValue -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name "AppsUseLightTheme" -ErrorAction SilentlyContinue)
        $theme = if ($useLight -eq 1) { "Light" } else { "Dark" }
    }

    $bc = New-Object System.Windows.Media.BrushConverter
    
    if ($theme -eq "Light") {
        $TargetWindow.Resources["WindowBg"] = $bc.ConvertFromString("#F3F3F3")
        $TargetWindow.Resources["PanelBg"] = $bc.ConvertFromString("#FFFFFF")
        $TargetWindow.Resources["InputBg"] = $bc.ConvertFromString("#FFFFFF")
        $TargetWindow.Resources["BorderBrush"] = $bc.ConvertFromString("#CCCCCC")
        $TargetWindow.Resources["PrimaryText"] = $bc.ConvertFromString("#000000")
        $TargetWindow.Resources["SecondaryText"] = $bc.ConvertFromString("#555555")
        $TargetWindow.Resources["NotesText"] = $bc.ConvertFromString("#006600")
        
        $global:LinkBrush = $bc.ConvertFromString("#0066CC")
        $global:TextBrush = $bc.ConvertFromString("#555555")
    } else {
        $TargetWindow.Resources["WindowBg"] = $bc.ConvertFromString("#1E1E1E")
        $TargetWindow.Resources["PanelBg"] = $bc.ConvertFromString("#252526")
        $TargetWindow.Resources["InputBg"] = $bc.ConvertFromString("#2D2D30")
        $TargetWindow.Resources["BorderBrush"] = $bc.ConvertFromString("#3F3F46")
        $TargetWindow.Resources["PrimaryText"] = $bc.ConvertFromString("#FFFFFF")
        $TargetWindow.Resources["SecondaryText"] = $bc.ConvertFromString("#CCCCCC")
        $TargetWindow.Resources["NotesText"] = $bc.ConvertFromString("#DCDCAA")
        
        $global:LinkBrush = $bc.ConvertFromString("#569CD6")
        $global:TextBrush = $bc.ConvertFromString("#CCCCCC")
    }
}