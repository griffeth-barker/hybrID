function Assert-GraphConnection {
    Set-Status "Checking Graph API authentication..." "#CA5100" 
    
    $context = Get-MgContext -ErrorAction SilentlyContinue
    if ($null -eq $context) {
        Set-Status "Waiting for Graph API sign-in..." "#CA5100"
        try {
            Connect-MgGraph -Scopes "User.Read.All", "Group.Read.All", "User-OnPremisesSyncBehavior.ReadWrite.All", "Group-OnPremisesSyncBehavior.ReadWrite.All" -NoWelcome
            Set-Status "Ready" "#007ACC" 
        } catch {
            Write-HybrIDLog -Source "Assert-GraphConnection" -Message "Graph sign-in failed or was canceled." -Exception $_.Exception
            Set-Status "Authentication failed or canceled." "#D03E3D" 
        }
    } else {
        Set-Status "Ready" "#007ACC"
    }
}