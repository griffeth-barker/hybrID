function Resolve-MailboxProfile {
    param(
        [int64]$RecipientTypeDetails,
        [string]$TargetAddress,
        [string]$HomeServerName
    )

    $profile = @{
        MailboxType = $null
        IsRemote = $false
        HasMailbox = $false
    }

    if (-not [string]::IsNullOrWhiteSpace($TargetAddress) -and $TargetAddress -like "*@*.mail.onmicrosoft.com") {
        $profile.IsRemote = $true
        $profile.HasMailbox = $true
    }

    if (-not [string]::IsNullOrWhiteSpace($HomeServerName)) {
        $profile.HasMailbox = $true
    }

    if (($RecipientTypeDetails -band 34359738368) -eq 34359738368 -or ($RecipientTypeDetails -band 4) -eq 4) {
        $profile.MailboxType = "Shared"
        $profile.HasMailbox = $true
    }
    elseif (($RecipientTypeDetails -band 8589934592) -eq 8589934592 -or ($RecipientTypeDetails -band 16) -eq 16) {
        $profile.MailboxType = "Room"
        $profile.HasMailbox = $true
    }
    elseif (($RecipientTypeDetails -band 17179869184) -eq 17179869184 -or ($RecipientTypeDetails -band 32) -eq 32) {
        $profile.MailboxType = "Equipment"
        $profile.HasMailbox = $true
    }
    elseif (($RecipientTypeDetails -band 2147483648) -eq 2147483648 -or ($RecipientTypeDetails -band 1) -eq 1) {
        $profile.MailboxType = "User"
        $profile.HasMailbox = $true
    }

    if (($RecipientTypeDetails -band 2147483648) -eq 2147483648 -or ($RecipientTypeDetails -band 34359738368) -eq 34359738368 -or ($RecipientTypeDetails -band 8589934592) -eq 8589934592 -or ($RecipientTypeDetails -band 17179869184) -eq 17179869184) {
        $profile.IsRemote = $true
    }

    return $profile
}
