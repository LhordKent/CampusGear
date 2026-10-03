# Run interactively on your computer. This script does not send email.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$taskProject = Join-Path $PSScriptRoot '..\CampusGear\CampusGear.WebApp\CampusGear.WebApp.csproj'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET SDK before running this setup.'
}

Write-Host 'Enable Google 2-Step Verification, then create an App Password named CampusGear.'
Write-Host 'https://myaccount.google.com/apppasswords'
$taskSenderEmail = (Read-Host 'Gmail address to send from').Trim()
try {
    $taskAddress = [System.Net.Mail.MailAddress]::new($taskSenderEmail)
} catch {
    throw 'Enter a valid email address.'
}
if ($taskAddress.Address -ne $taskSenderEmail -or $taskSenderEmail -match '[\r\n]') {
    throw 'Enter only the email address, without a display name.'
}

$taskSecurePassword = Read-Host 'Google App Password (hidden; not your usual Gmail password)' -AsSecureString
$taskAppPassword = $null
$taskSecrets = $null
$taskJson = $null
try {
    $taskAppPassword = [System.Net.NetworkCredential]::new('', $taskSecurePassword).Password -replace '\s', ''
    if ($taskAppPassword -notmatch '^[A-Za-z0-9]{16}$') {
        throw 'The Google App Password must contain 16 characters. Spaces may be included when pasting.'
    }
    $taskSecrets = @{
        'Email:Provider' = 'Smtp'
        'Email:Smtp:Host' = 'smtp.gmail.com'
        'Email:Smtp:Port' = '587'
        'Email:Smtp:EnableSsl' = 'true'
        'Email:Smtp:From' = $taskSenderEmail
        'Email:Smtp:Username' = $taskSenderEmail
        'Email:Smtp:Password' = $taskAppPassword
    }
    $taskJson = $taskSecrets | ConvertTo-Json -Compress
    # Pass secrets through standard input, never as process arguments or a repo file.
    $taskJson | & dotnet user-secrets set --project $taskProject
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not save SMTP settings. Check the .NET SDK and project path.'
    }
} finally {
    $taskAppPassword = $null
    $taskSecrets = $null
    $taskJson = $null
    $taskSecurePassword.Dispose()
}

Write-Host 'Saved locally in .NET User Secrets, outside the repository. The local store is not encrypted.'
if (Test-Path Env:Email__Provider) {
    Write-Warning 'Email__Provider is set in this terminal and overrides the saved provider. Remove it to use these settings.'
}
Write-Host 'Restart CampusGear in Development, then sign up with an email you control to test delivery.'
