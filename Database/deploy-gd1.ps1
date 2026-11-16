<#
.SYNOPSIS
    Triển khai tuần tự các script GĐ1 (00..09 + 15_seed_data.sql) vào SQL Server.
    Đọc mật khẩu từ .NET User Secrets (không lưu mật khẩu trong repo).
#>
param(
    [string]$Server = "."
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dbDir = $PSScriptRoot

function Get-PasswordFromConnStr([string]$connStr) {
    if ($connStr -match 'Password=([^;]+)') {
        return $Matches[1]
    }
    throw "Không tìm thấy Password trong connection string từ User Secrets."
}

function New-StrongPassword {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 15
    $rng.GetBytes($bytes)
    return "Sms!" + [Convert]::ToBase64String($bytes).Replace('=', '9').Replace('+', 'A').Replace('/', 'b')
}

# Đọc User Secrets của M03_Opus
$secretLines = dotnet user-secrets list --project (Join-Path $root "M03_Opus.csproj")
$secrets = @{}
foreach ($line in $secretLines) {
    if ($line -match '^([^=]+?)\s*=\s*(.+)$') {
        $secrets[$Matches[1]] = $Matches[2]
    }
}

$smsAppPwd = Get-PasswordFromConnStr $secrets["ConnectionStrings:SmsApp"]
$smsAppAdminPwd = Get-PasswordFromConnStr $secrets["ConnectionStrings:SmsAppAdmin"]
$smsLogPwd = Get-PasswordFromConnStr $secrets["ConnectionStrings:SmsLog"]

$otherKeys = @(
    "SqlLogins:SmsReader",
    "SqlLogins:SmsEntry",
    "SqlLogins:SmsAuditor",
    "SqlLogins:SmsStaffCntt",
    "SqlLogins:SmsStaffKt",
    "SqlLogins:SmsBackup",
    "SqlLogins:SmsMigrator",
    "SqlLogins:SmsLab"
)

foreach ($k in $otherKeys) {
    if (-not $secrets.ContainsKey($k)) {
        $pwd = New-StrongPassword
        dotnet user-secrets set --project (Join-Path $root "M03_Opus.csproj") $k $pwd | Out-Null
        $secrets[$k] = $pwd
    }
}

$scriptsPre = @(
    "00_server_hardening.sql",
    "01_create_database.sql",
    "02_schemas_tables.sql",
    "03_identity_tables.sql",
    "04_views.sql",
    "05_procedures.sql"
)

foreach ($s in $scriptsPre) {
    Write-Host "Running $s..." -ForegroundColor Cyan
    sqlcmd -S $Server -E -b -I -i (Join-Path $dbDir $s)
    if ($LASTEXITCODE -ne 0) { throw "Script $s thất bại." }
}

Write-Host "Running 06_logins_users_roles.sql..." -ForegroundColor Cyan
sqlcmd -S $Server -E -b -I -i (Join-Path $dbDir "06_logins_users_roles.sql") `
    -v SmsAppPwd="$smsAppPwd" `
       SmsAppAdminPwd="$smsAppAdminPwd" `
       SmsLogWriterPwd="$smsLogPwd" `
       SmsReaderPwd="$($secrets['SqlLogins:SmsReader'])" `
       SmsEntryPwd="$($secrets['SqlLogins:SmsEntry'])" `
       SmsAuditorPwd="$($secrets['SqlLogins:SmsAuditor'])" `
       SmsStaffCnttPwd="$($secrets['SqlLogins:SmsStaffCntt'])" `
       SmsStaffKtPwd="$($secrets['SqlLogins:SmsStaffKt'])" `
       SmsBackupPwd="$($secrets['SqlLogins:SmsBackup'])" `
       SmsMigratorPwd="$($secrets['SqlLogins:SmsMigrator'])" `
       SmsLabPwd="$($secrets['SqlLogins:SmsLab'])"
if ($LASTEXITCODE -ne 0) { throw "Script 06_logins_users_roles.sql thất bại." }

$scriptsPost = @(
    "07_permissions_grant_deny.sql",
    "08_rls.sql",
    "09_ddm.sql",
    "15_seed_data.sql"
)

foreach ($s in $scriptsPost) {
    Write-Host "Running $s..." -ForegroundColor Cyan
    sqlcmd -S $Server -E -b -I -i (Join-Path $dbDir $s)
    if ($LASTEXITCODE -ne 0) { throw "Script $s thất bại." }
}

Write-Host "Triển khai GĐ1 thành công!" -ForegroundColor Green
