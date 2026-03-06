function Set-Link ($TextBlock, $Text, $Url) {
    $TextBlock.Inlines.Clear()

    if ($Url) {
        $link = New-Object System.Windows.Documents.Hyperlink
        $run = New-Object System.Windows.Documents.Run($Text)
        $link.Inlines.Add($run)
        $link.NavigateUri = [System.Uri]::new($Url)
        $link.TextDecorations = "Underline"
        
        # Inject global theme brush
        $link.Foreground = $global:LinkBrush
        
        $link.Add_RequestNavigate({
            if ($_.Uri) { 
                Open-UrlInNewWindow $_.Uri.AbsoluteUri 
                $_.Handled = $true
            }
        })
        
        $TextBlock.Inlines.Add($link)
    } else {
        $TextBlock.Text = $Text
        
        # Inject global theme brush
        $TextBlock.Foreground = $global:TextBrush
    }
}