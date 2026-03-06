function Set-Status ($Text, $ColorHex) {
    $txtStatus.Text = $Text
    $statusBar.Background = $brushConverter.ConvertFromString($ColorHex)
    [System.Windows.Forms.Application]::DoEvents() 
}