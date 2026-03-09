function Invoke-SoaTransfer {
    $searchContext = $global:CurrentSearchContext

    if (-not $searchContext) {
        Set-Status "Search for a synced AD user first." "#D03E3D"
        return
    }

    if (-not $searchContext.MgObject -or -not $searchContext.AdObject) {
        Set-Status "Search for a synced AD user first." "#D03E3D"
        return
    }

    $mgObject = $searchContext.MgObject
    $currentState = $searchContext.IsCloudManaged

    $targetState = $true
    if ($currentState -eq $true) {
        $targetState = $false
    }

    $actionText = "transfer SOA to Entra ID"
    if (-not $targetState) {
        $actionText = "revert SOA to on-premises"
    }

    $msg = "Are you sure you want to " + $actionText + " for '" + $mgObject.DisplayName + "'?`n`nThis change impacts where the object is managed."
    $confirmResult = [System.Windows.Forms.MessageBox]::Show($msg, "Confirm SOA Change", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Warning)

    if ($confirmResult -ne [System.Windows.Forms.DialogResult]::Yes) {
        Set-Status "SOA change canceled." "#CA5100"
        return
    }

    try {
        Set-Status "Applying SOA change..." "#CA5100"

        if ($targetState) {
            $patchBody = '{"isCloudManaged":true}'
        }
        else {
            $patchBody = '{"isCloudManaged":false}'
        }
        $patchUri = "https://graph.microsoft.com/v1.0/users/" + $mgObject.Id + "/onPremisesSyncBehavior"
        Invoke-MgGraphRequest -Method PATCH -Uri $patchUri -Body $patchBody -ContentType "application/json" -ErrorAction Stop | Out-Null

        Start-Sleep -Milliseconds 250

        $getUri = "https://graph.microsoft.com/v1.0/users/" + $mgObject.Id + "/onPremisesSyncBehavior?`$select=isCloudManaged"
        $soaResponse = Invoke-MgGraphRequest -Method GET -Uri $getUri -ErrorAction Stop

        if ($soaResponse.PSObject.Properties.Name -contains "isCloudManaged") {
            $searchContext.IsCloudManaged = $soaResponse.isCloudManaged
            Set-Variable -Scope Global -Name CurrentSearchContext -Value $searchContext
        }

        if ($searchContext.IsCloudManaged -eq $true) {
            $txtSoaState.Text = "Cloud-managed (Entra ID)"
            $btnTransferSoa.Content = "Revert SOA to On-Premises"
            Set-Status "SOA updated: object is now cloud-managed." "#16825D"
            if (-not [string]::IsNullOrWhiteSpace($valNotes.Text)) {
                $valNotes.Text = $valNotes.Text + "`n`nSOA switched to cloud-managed (Entra ID)."
            }
            else {
                $valNotes.Text = "SOA switched to cloud-managed (Entra ID)."
            }
        }
        elseif ($searchContext.IsCloudManaged -eq $false) {
            $txtSoaState.Text = "On-premises managed (AD DS)"
            $btnTransferSoa.Content = "Transfer SOA to Entra ID"
            Set-Status "SOA updated: object is now on-premises-managed (after next sync cycle takeover)." "#16825D"
            if (-not [string]::IsNullOrWhiteSpace($valNotes.Text)) {
                $valNotes.Text = $valNotes.Text + "`n`nSOA reverted to on-premises management."
            }
            else {
                $valNotes.Text = "SOA reverted to on-premises management."
            }
        }
        else {
            $txtSoaState.Text = "Unknown (check Graph permission)"
            $btnTransferSoa.Content = "Refresh/Transfer SOA"
            Set-Status "SOA request sent. Unable to verify current state." "#CA5100"
        }
    }
    catch {
        Write-HybrIDLog -Source "Invoke-SoaTransfer" -Message "SOA transfer operation failed." -Exception $_.Exception -Context @{ UserId = $mgObject.Id; TargetIsCloudManaged = $targetState }
        Set-Status "SOA change failed. Confirm Graph permissions and role assignments." "#D03E3D"
    }
}

