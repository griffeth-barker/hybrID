function Assert-GraphConnection {
    Set-Status "Checking Graph API authentication..." "#CA5100" 
    
    $context = Get-MgContext -ErrorAction SilentlyContinue
    if ($null -eq $context) {
        Set-Status "Waiting for Graph API sign-in..." "#CA5100"
        try {
            Connect-MgGraph -Scopes "User.Read.All", "Group.Read.All" -NoWelcome
            Set-Status "Ready" "#007ACC" 
        } catch {
            Set-Status "Authentication failed or canceled." "#D03E3D" 
        }
    } else {
        Set-Status "Ready" "#007ACC"
    }
}