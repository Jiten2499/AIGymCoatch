<#
.SYNOPSIS
    Generates the PasswordHash and PasswordSalt for a LifeFit AI user, matching the
    application's Pbkdf2PasswordHasher exactly (PBKDF2-SHA256).

.DESCRIPTION
    The API stores passwords as PBKDF2-SHA256 with a per-user random salt. Hash and salt
    are persisted as Base64 strings in app.Users.PasswordHash / app.Users.PasswordSalt.

    These parameters MUST match the running deployment's "Password" configuration section
    (backend/src/Api/appsettings.json). The production defaults are:
        Iterations = 210000, SaltSize = 16 bytes, HashSize = 32 bytes.
    If the deployment overrides Password:Iterations (e.g. via env var Password__Iterations),
    pass the same value with -Iterations or the generated hash will not verify at login.

    Prefer running from an already-open PowerShell window:
        cd F:\Projects\AIGymCoatch\database\scripts
        .\generate-super-admin-hash.ps1
    Double-click also works; this script waits for Enter before the window closes.

.EXAMPLE
    .\generate-super-admin-hash.ps1

.EXAMPLE
    .\generate-super-admin-hash.ps1 -Iterations 210000

.NOTES
    Works in Windows PowerShell 5.1 and PowerShell 7+.
    The chosen password must satisfy the app policy: at least 12 characters and include
    uppercase, lowercase, a digit, and a special character.
#>
[CmdletBinding()]
param(
    [int]$Iterations = 210000,
    [int]$SaltSize = 16,
    [int]$HashSize = 32
)

$ErrorActionPreference = 'Stop'
$password = $null
$passwordConfirm = $null

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString]$Secure)
    if ($null -eq $Secure -or $Secure.Length -eq 0) {
        return ''
    }

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

try {
    Write-Host 'LifeFit AI SuperAdmin password hash generator' -ForegroundColor Cyan
    Write-Host 'Password is hidden while you type. It must be at least 12 characters and include uppercase, lowercase, a digit, and a special character.'
    Write-Host ''

    $secure = Read-Host -AsSecureString -Prompt 'Enter the SuperAdmin password'
    $confirm = Read-Host -AsSecureString -Prompt 'Confirm the SuperAdmin password'
    $password = ConvertFrom-SecureStringPlain -Secure $secure
    $passwordConfirm = ConvertFrom-SecureStringPlain -Secure $confirm

    if ([string]::IsNullOrEmpty($password)) {
        throw 'Password cannot be empty.'
    }

    if ($password -cne $passwordConfirm) {
        throw 'Passwords do not match.'
    }

    if ($password.Length -lt 12 -or
        $password -cnotmatch '[A-Z]' -or
        $password -cnotmatch '[a-z]' -or
        $password -notmatch '[0-9]' -or
        $password -notmatch '[^A-Za-z0-9]') {
        throw 'Password must be at least 12 characters and include uppercase, lowercase, a digit, and a special character.'
    }

    $salt = New-Object byte[] $SaltSize
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($salt)
    }
    finally {
        $rng.Dispose()
    }

    # Same construction the .NET API uses: PBKDF2 with SHA-256.
    $pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $password,
        $salt,
        $Iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        $hash = $pbkdf2.GetBytes($HashSize)
    }
    finally {
        $pbkdf2.Dispose()
    }

    Write-Host ''
    Write-Host 'Paste these two values into create-super-admin.sql:' -ForegroundColor Green
    Write-Host ''
    Write-Host ("  PasswordHash = N'{0}'" -f [Convert]::ToBase64String($hash))
    Write-Host ("  PasswordSalt = N'{0}'" -f [Convert]::ToBase64String($salt))
    Write-Host ''
    Write-Host ("  (iterations={0}, saltSize={1}, hashSize={2})" -f $Iterations, $SaltSize, $HashSize) -ForegroundColor DarkGray
}
catch {
    Write-Host ''
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ''
    Write-Host 'Nothing was written. Fix the issue above and run the script again.' -ForegroundColor Yellow
    exit 1
}
finally {
    $password = $null
    $passwordConfirm = $null
    Write-Host ''
    Read-Host 'Press Enter to close this window'
}
