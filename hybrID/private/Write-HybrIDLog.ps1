function Write-HybrIDLog {
    param(
        [string]$Source,
        [string]$Message,
        [System.Exception]$Exception,
        [hashtable]$Context
    )

    try {
        $logsDir = $global:logsDir
        if ([string]::IsNullOrWhiteSpace($logsDir)) {
            if ($global:configDir) {
                $logsDir = Join-Path -Path (Split-Path -Path $global:configDir -Parent) -ChildPath "logs"
            } else {
                $logsDir = Join-Path -Path (Get-Location).Path -ChildPath "logs"
            }
        }

        if (-not (Test-Path -Path $logsDir)) {
            New-Item -Path $logsDir -ItemType Directory -Force | Out-Null
        }

        try {
            $dirItem = Get-Item -Path $logsDir -ErrorAction Stop
            if (-not ($dirItem.Attributes -band [System.IO.FileAttributes]::Hidden)) {
                $dirItem.Attributes = $dirItem.Attributes -bor [System.IO.FileAttributes]::Hidden
            }
        } catch {}

        $logPath = Join-Path -Path $logsDir -ChildPath ("hybrID-" + (Get-Date -Format "yyyy-MM-dd") + ".log")

        $contextText = ""
        if ($Context) {
            $pairs = @()
            foreach ($key in $Context.Keys) {
                $pairs += ($key + "=" + [string]$Context[$key])
            }
            if ($pairs.Count -gt 0) {
                $contextText = " | Context: " + ($pairs -join "; ")
            }
        }

        $exceptionText = ""
        if ($Exception) {
            $exceptionText = " | Exception: " + $Exception.Message
        }

        $line = (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff") + " | ERROR | " + $Source + " | " + $Message + $exceptionText + $contextText
        Add-Content -Path $logPath -Value $line -Encoding UTF8
    } catch {}
}
