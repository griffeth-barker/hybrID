function Invoke-Search {
    $identity = $txtSearch.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($identity)) {
        Set-Status "Enter an identity to search." "#D03E3D"
        return
    }

    $global:CurrentSearchContext = $null

    Clear-Fields
    Set-Status "Searching Active Directory and Graph API for '$identity'..." "#CA5100"
    
    $adObject = $null
    $mgObject = $null
    $upnToSearchInGraph = $identity 

    try {
        $adObject = Get-ADObject -Filter {sAMAccountName -eq $identity -or ObjectGUID -eq $identity -or UserPrincipalName -eq $identity} -Properties targetAddress, msExchHomeServerName, mail, groupType, UserPrincipalName -ErrorAction Stop | Select-Object -First 1
        if ($adObject -and $adObject.UserPrincipalName) { 
            $upnToSearchInGraph = $adObject.UserPrincipalName 
        }
    } catch {
        Write-HybrIDLog -Source "Invoke-Search.AD" -Message "Active Directory lookup failed." -Exception $_.Exception -Context @{ Identity = $identity }
    }

    try {
        $mgObject = Get-MgUser -UserId $upnToSearchInGraph -ErrorAction SilentlyContinue
        if (-not $mgObject) {
            $mgObject = Get-MgGroup -Filter "mail eq '$identity' or displayName eq '$identity'" -ErrorAction SilentlyContinue | Select-Object -First 1
        }
    } catch {
        Write-HybrIDLog -Source "Invoke-Search.Graph" -Message "Graph user/group lookup failed." -Exception $_.Exception -Context @{ Identity = $identity; GraphLookup = $upnToSearchInGraph }
    }

    $entraUrl = $null
    $exoUrl = $null
    $onPremExchUrl = $null

    # Get the FQDN from the JSON settings
    $fqdn = $global:Config.Settings.OnPremExchangeFqdn

    if ($mgObject) {
        if ($mgObject.AdditionalProperties["@odata.type"] -like "*user*") {
            $entraUrl = $global:Config.ManagementUrls.EntraUser -replace '\{id\}', $mgObject.Id
            $exoUrl   = $global:Config.ManagementUrls.ExoMailbox -replace '\{id\}', $mgObject.Id
        } else {
            $entraUrl = $global:Config.ManagementUrls.EntraGroup -replace '\{id\}', $mgObject.Id
            $exoUrl   = $global:Config.ManagementUrls.ExoGroup -replace '\{id\}', $mgObject.Id
        }
    }

    if ($adObject) {
        if ($adObject.ObjectClass -eq "user") {
            $onPremExchUrl = $global:Config.ManagementUrls.OnPremEcpMailbox -replace '\{fqdn\}', $fqdn -replace '\{id\}', $adObject.ObjectGUID
        } elseif ($adObject.ObjectClass -eq "group") {
            $onPremExchUrl = $global:Config.ManagementUrls.OnPremEcpGroup -replace '\{fqdn\}', $fqdn -replace '\{id\}', $adObject.ObjectGUID
        }
    }

    if ($adObject) {
        Set-Status "Object located in Active Directory." "#16825D"
        
        $valName.Text = $adObject.Name
        $valClass.Text = $adObject.ObjectClass
        
        Set-Link -TextBlock $txtManageIdentity -Text "On-Premises Active Directory" -Url $null

        if ($adObject.ObjectClass -eq "user") {
            if ($adObject.targetAddress -like "*@*.mail.onmicrosoft.com") {
                Set-Link -TextBlock $txtManageExchange -Text "Exchange Server ECP" -Url $onPremExchUrl
                $valNotes.Text = "Mailbox is in EXO, but managed as a Remote Mailbox via On-Premises Exchange."
            } elseif ($adObject.msExchHomeServerName) {
                Set-Link -TextBlock $txtManageExchange -Text "Exchange Server ECP" -Url $onPremExchUrl
                $valNotes.Text = "Mailbox resides entirely on-premises."
            } else {
                Set-Link -TextBlock $txtManageExchange -Text "No mailbox detected" -Url $null
            }
        }
        elseif ($adObject.ObjectClass -eq "group") {
            if ($adObject.mail) {
                Set-Link -TextBlock $txtManageExchange -Text "Exchange Server ECP" -Url $onPremExchUrl
                $valNotes.Text = "Mail-enabled group. Manage exchange properties via On-Prem ECP."
            } else {
                Set-Link -TextBlock $txtManageExchange -Text "N/A" -Url $null
            }
        }

        if ($mgObject) {
            $valNotes.Text += "`n`nSync Status: Synced to Entra ID."
            Set-Link -TextBlock $txtManageIdentity -Text "Active Directory" -Url $null
        }

        $isSyncedUser = ($adObject.ObjectClass -eq "user" -and $mgObject -and $mgObject.AdditionalProperties["@odata.type"] -like "*user*")
        if ($isSyncedUser) {
            $global:CurrentSearchContext = @{
                Identity = $identity
                AdObject = $adObject
                MgObject = $mgObject
                IsCloudManaged = $null
            }

            try {
                $soaGetUri = "https://graph.microsoft.com/v1.0/users/$($mgObject.Id)/onPremisesSyncBehavior?`$select=isCloudManaged"
                $soaResponse = Invoke-MgGraphRequest -Method GET -Uri $soaGetUri -ErrorAction Stop
                if ($soaResponse.PSObject.Properties.Name -contains "isCloudManaged") {
                    $global:CurrentSearchContext.IsCloudManaged = $soaResponse.isCloudManaged
                }
            } catch {
                Write-HybrIDLog -Source "Invoke-Search.SOA" -Message "SOA state lookup failed." -Exception $_.Exception -Context @{ Identity = $identity; UserId = $mgObject.Id }
            }

            if ($global:CurrentSearchContext.IsCloudManaged -eq $true) {
                $txtSoaState.Text = "Cloud-managed (Entra ID)"
                $btnTransferSoa.Content = "Revert SOA to On-Premises"
            } elseif ($global:CurrentSearchContext.IsCloudManaged -eq $false) {
                $txtSoaState.Text = "On-premises managed (AD DS)"
                $btnTransferSoa.Content = "Transfer SOA to Entra ID"
            } else {
                $txtSoaState.Text = "Unknown (check Graph permission)"
                $btnTransferSoa.Content = "Refresh/Transfer SOA"
            }

            $btnTransferSoa.Visibility = "Visible"
        } else {
            $txtSoaState.Text = "N/A"
            $btnTransferSoa.Visibility = "Collapsed"
        }

    } elseif ($mgObject) {
        Set-Status "Object located in Entra ID." "#16825D"
        $valName.Text = $mgObject.DisplayName
        
        if ($mgObject.AdditionalProperties["@odata.type"] -like "*user*") {
            $valClass.Text = "Cloud User"
        } else {
            $valClass.Text = "Cloud Group"
        }
        
        Set-Link -TextBlock $txtManageIdentity -Text "Entra ID" -Url $entraUrl
        Set-Link -TextBlock $txtManageExchange -Text "Exchange Online" -Url $exoUrl
        $txtSoaState.Text = "N/A"
        $btnTransferSoa.Visibility = "Collapsed"
        
        $valNotes.Text = "This object does not exist in Active Directory. Manage it entirely in M365/Entra ID."
    } else {
        Set-Status "Object not found in Active Directory or Entra ID." "#D03E3D"
        $txtSoaState.Text = "N/A"
        $btnTransferSoa.Visibility = "Collapsed"
    }
}