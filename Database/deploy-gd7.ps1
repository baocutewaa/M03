<#
.SYNOPSIS
    Triển khai GĐ7: Audit, TDE (SMS & SMS_Log), Backup mã hóa, SQL Agent Jobs, Logon Trigger & NTFS ACLs.
    Đọc/sinh mật khẩu bảo vệ certificate từ .NET User Secrets (không hardcode mật khẩu).
#>
param(
    [string]$Server = "."
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dbDir = $PSScriptRoot
$csproj = Join-Path $root "M03_Opus.csproj"

function New-StrongPassword {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 18
    $rng.GetBytes($bytes)
    return "Key!" + [Convert]::ToBase64String($bytes).Replace('=', '9').Replace('+', 'A').Replace('/', 'b')
}

# 1) Tạo thư mục & siết quyền NTFS (từ chối Users / Authenticated Users)
$dirs = @("C:\SqlAudit\SMS", "C:\SqlBackup\SMS", "C:\SqlKeys")
foreach ($d in $dirs) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Path $d -Force | Out-Null
    }
    icacls $d /inheritance:r /grant:r "NT SERVICE\MSSQLSERVER:(OI)(CI)F" "NT SERVICE\SQLSERVERAGENT:(OI)(CI)M" "NT AUTHORITY\SYSTEM:(OI)(CI)F" "BUILTIN\Administrators:(OI)(CI)F" | Out-Null
}

# 2) Đọc hoặc khởi tạo khóa trong User Secrets
$secretLines = dotnet user-secrets list --project $csproj
$secrets = @{}
foreach ($line in $secretLines) {
    if ($line -match '^([^=]+?)\s*=\s*(.+)$') {
        $secrets[$Matches[1]] = $Matches[2]
    }
}

foreach ($k in @("SqlKeys:MasterKey", "SqlKeys:TdeCert", "SqlKeys:BackupCert")) {
    if (-not $secrets.ContainsKey($k)) {
        $pwd = New-StrongPassword
        dotnet user-secrets set --project $csproj $k $pwd | Out-Null
        $secrets[$k] = $pwd
    }
}

$sqlVars = @(
    "MasterKeyPwd=$($secrets['SqlKeys:MasterKey'])",
    "TdeKeyPwd=$($secrets['SqlKeys:TdeCert'])",
    "BackupKeyPwd=$($secrets['SqlKeys:BackupCert'])"
)

# 3) Chạy tuần tự các script GĐ7
$scripts = @(
    "10_audit.sql",
    "11_tde.sql",
    "12_backup_jobs.sql",
    "14_logon_trigger.sql",
    "04_views.sql",
    "18_login_audit_review.sql",
    "verify\verify_gd7.sql"
)

foreach ($s in $scripts) {
    $path = Join-Path $dbDir $s
    Write-Host "Running $s..." -ForegroundColor Cyan
    sqlcmd -S $Server -E -b -I -f 65001 -i $path `
        -v MasterKeyPwd="$($secrets['SqlKeys:MasterKey'])" `
           TdeKeyPwd="$($secrets['SqlKeys:TdeCert'])" `
           BackupKeyPwd="$($secrets['SqlKeys:BackupCert'])"
    if ($LASTEXITCODE -ne 0) {
        throw "Script $s failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Hoan tat trien khai & kiem tra GD7." -ForegroundColor Green
