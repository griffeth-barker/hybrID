function Open-UrlInNewWindow ($Url) {
    try {
        $progId = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice' -ErrorAction Stop).ProgId
        $command = (Get-ItemProperty "Registry::HKEY_CLASSES_ROOT\$progId\shell\open\command" -ErrorAction Stop).'(default)'
        
        $exePath = if ($command -match '^"([^"]+)"') { $matches[1] } else { $command.Split(' ')[0] }
        
        if (Test-Path $exePath) {
            $exeName = Split-Path $exePath -Leaf
            if ($exeName -match "chrome|msedge|brave|opera") {
                Start-Process $exePath -ArgumentList "--new-window `"$Url`""
                return
            } elseif ($exeName -match "firefox") {
                Start-Process $exePath -ArgumentList "-new-window `"$Url`""
                return
            }
        }
    } catch {}
    
    Start-Process $Url
}