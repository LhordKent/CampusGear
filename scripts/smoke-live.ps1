param([string]$BaseUrl = 'http://127.0.0.1:5077', [switch]$KeepFixtures,
    [string]$DatabaseConnectionString = 'Server=.\SQLEXPRESS;Database=CampusGear;Integrated Security=True;TrustServerCertificate=True')
$ErrorActionPreference = 'Stop'
$targetUri = [Uri]$BaseUrl
if (!$targetUri.IsLoopback) { throw 'This smoke script is restricted to the local Development app.' }
$run = [Guid]::NewGuid().ToString('N')
$checks = [System.Collections.Generic.List[string]]::new()
$successful = $false
$fixtureUsers = [System.Collections.Generic.List[string]]::new()
$fixtureItems = [System.Collections.Generic.List[Guid]]::new()
$fixtureCategories = [System.Collections.Generic.List[Guid]]::new()
$connection = [System.Data.SqlClient.SqlConnection]::new($DatabaseConnectionString)
$connection.Open()
function Sql([string]$text, [hashtable]$values = @{}) {
    $command = $connection.CreateCommand(); $command.CommandText = $text
    foreach ($entry in $values.GetEnumerator()) { [void]$command.Parameters.AddWithValue('@' + $entry.Key, $entry.Value) }
    try { $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Client {
    $handler = [System.Net.Http.HttpClientHandler]::new(); $handler.AllowAutoRedirect = $false
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    [System.Net.Http.HttpClient]::new($handler)
}
function Http($client, [string]$path, [hashtable]$form = $null) {
    if ($null -eq $form) { $response = $client.GetAsync($BaseUrl + $path).GetAwaiter().GetResult() }
    else {
        $pairs = [System.Collections.Generic.Dictionary[string,string]]::new()
        foreach ($entry in $form.GetEnumerator()) { $pairs.Add($entry.Key, [string]$entry.Value) }
        $body = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
        $response = $client.PostAsync($BaseUrl + $path, $body).GetAwaiter().GetResult()
    }
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $result = [pscustomobject]@{ Status = [int]$response.StatusCode; Location = [string]$response.Headers.Location; Body = $content }
    $response.Dispose()
    if ($result.Status -eq 302) {
        $location = $result.Location
        if ([string]::IsNullOrWhiteSpace($location)) { throw "Redirect without destination from $path" }
        for ($hop=0; $hop -lt 5; $hop++) {
            $uri = [Uri]::new([Uri]$BaseUrl, $location)
            if ($uri.Authority -ne ([Uri]$BaseUrl).Authority) { throw "External redirect from $path" }
            $landing = $client.GetAsync($uri).GetAwaiter().GetResult()
            $status = [int]$landing.StatusCode; $location=[string]$landing.Headers.Location
            $landing.Dispose()
            if ($status -ne 302) { break }
        }
        if ($status -ge 400 -or $status -eq 302) { throw "Redirect from $path reached HTTP $status" }
        $checks.Add("Redirect destination $($result.Location)")
    }
    $result
}
function Input([string]$html, [string]$name) {
    $tag = [regex]::Match($html, '<input\b[^>]*\bname="' + [regex]::Escape($name) + '"[^>]*>').Value
    [System.Net.WebUtility]::HtmlDecode([regex]::Match($tag, '\bvalue="([^"]*)"').Groups[1].Value)
}
function Post($client, [string]$path, [hashtable]$form, [string]$source = $path) {
    $page = Http $client $source; $form['__RequestVerificationToken'] = Input $page.Body '__RequestVerificationToken'
    Http $client $path $form
}
function Check([bool]$ok, [string]$name) {
    if (!$ok) { throw "FAILED: $name" }; $checks.Add($name)
}
function Code([string]$email) {
    $mail = (Http $anonymous '/auth/development-inbox').Body | ConvertFrom-Json
    $message = @($mail | Where-Object { $_.toEmail -eq $email })[0]
    [regex]::Match($message.body, '\b\d{6}\b').Value
}
function Account([string]$name) {
    $client = Client; $email = "$name-$run@example.test"; $password = 'LocalTest!' + $run
    $result = Post $client '/auth/signup' @{ FullName = "Smoke $name"; Email = $email; MobileNumber = '09123456789'; Password = $password; ConfirmPassword = $password }
    Check ($result.Status -eq 302 -and $result.Location -eq '/auth/email-verification') "$name signup"
    $id = [string](Sql 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}); $fixtureUsers.Add($id)
    $result = Post $client '/auth/email-verification' @{ Code = (Code $email) }
    Check ($result.Status -eq 302) "$name verified"
    [pscustomobject]@{Client=$client; Email=$email; Password=$password; Id=$id}
}
function Login($account, [bool]$administrator = $false) {
    $client = Client
    $result = Post $client '/auth/login' @{ Email=$account.Email; Password=$account.Password }
    if ($administrator) {
        Check ($result.Location -eq '/auth/two-factor') 'Administrator password requires code'
        $result = Post $client '/auth/two-factor' @{Code=(Code $account.Email)}
    }
    Check ($result.Status -eq 302 -and $result.Location -match '/dashboard$') 'Verified login'
    $client
}
function UserForm($client, [string]$id, [string]$role, [bool]$eligible = $true, [bool]$active = $true, [string]$student = '') {
    $page = Http $client "/admin/users/$id/edit"
    @{'__RequestVerificationToken'=(Input $page.Body '__RequestVerificationToken'); ConcurrencyStamp=(Input $page.Body 'ConcurrencyStamp'); ProfileRowVersion=(Input $page.Body 'ProfileRowVersion'); FullName=(Input $page.Body 'FullName'); Role=$role; IsActive=$active.ToString(); IsEligible=$eligible.ToString(); StudentNumber=$student; Department='Test department'; EligibilityNotes=''}
}
$anonymous = Client
try {
    if ((Http $anonymous '/auth/development-inbox').Status -ne 200) { throw 'The Development-only inbox must be available before creating test fixtures.' }
    $admin = Account 'admin'; $borrower = Account 'borrower'
    [void](Sql "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles (UserId,RoleId) SELECT @id,Id FROM AspNetRoles WHERE Name='Administrator'; UPDATE AspNetUsers SET SecurityStamp=NEWID(),ConcurrencyStamp=NEWID() WHERE Id=@id" @{id=$admin.Id})
    $staff = Login $admin $true; $borrow = $borrower.Client
    foreach ($route in @('/borrower/dashboard','/borrower/reservation','/borrower/history','/borrower/calendar')) {
        Check ((Http $borrow $route).Status -eq 200) "Borrower route $route"
    }
    foreach ($route in @('/admin/dashboard','/admin/equipment','/admin/categories','/admin/users','/admin/borrowers','/admin/audit','/admin/maintenance','/admin/reservations','/admin/approvals','/admin/returns','/admin/history','/admin/calendar')) {
        $result = Http $staff $route
        Check ($result.Status -eq 200 -or ($route -eq '/admin/borrowers' -and $result.Status -eq 302)) "Administrator route $route ($($result.Status) $($result.Location))"
    }
    Check ((Http $borrow '/admin/equipment').Location -match '/auth/access-denied') 'Borrower denied administrator route'
    Check ((Http $anonymous '/admin/users').Location -match '/auth/login') 'Anonymous denied protected route'
    foreach ($spec in @(@($borrow,'/borrower/calendar'),@($borrow,'/borrower/history'),@($staff,'/admin/calendar'),@($staff,'/admin/audit'),@($staff,'/admin/users'),@($staff,'/admin/approvals'))) {
        $page = Http $spec[0] $spec[1]
        $links = [regex]::Matches($page.Body, '<a\b[^>]*\bhref="([^"]*)"') | ForEach-Object { [System.Net.WebUtility]::HtmlDecode($_.Groups[1].Value) } | Select-Object -Unique
        foreach ($link in $links) {
            Check (![string]::IsNullOrWhiteSpace($link)) "Nonempty navigation link in $($spec[1])"
            if ($link.StartsWith('/')) { Check ((Http $spec[0] $link).Status -in 200,302) "Navigation destination $link" }
        }
    }
    $categoryName = 'Smoke category ' + $run
    $result = Post $staff '/admin/categories/save' @{'Input.Name'=$categoryName;'Input.IsActive'='true'} '/admin/categories'
    $categoryId = [Guid](Sql 'SELECT Id FROM EquipmentCategories WHERE Name=@name' @{name=$categoryName}); $fixtureCategories.Add($categoryId)
    Check ($result.Status -eq 302) 'Category create'
    $assetCode = 'SMOKE-' + $run
    $result = Post $staff '/admin/equipment/save' @{'Input.CategoryId'=$categoryId;'Input.Name'='Smoke Camera';'Input.AssetCode'=$assetCode;'Input.Condition'='Good';'Input.IsActive'='true'} '/admin/equipment'
    $itemId = [Guid](Sql 'SELECT Id FROM EquipmentItems WHERE AssetCode=@code' @{code=$assetCode}); $fixtureItems.Add($itemId)
    Check ($result.Status -eq 302) 'Equipment create'
    $formPage = Http $borrow '/borrower/reservation'
    $start = [DateTime]::UtcNow.AddHours(8).AddMinutes(10); $end=$start.AddHours(1)
    $reservationForm = @{ '__RequestVerificationToken'=(Input $formPage.Body '__RequestVerificationToken'); RequestId=(Input $formPage.Body 'RequestId'); EquipmentItemId=$itemId; StartLocal=$start.ToString('yyyy-MM-ddTHH:mm'); EndLocal=$end.ToString('yyyy-MM-ddTHH:mm'); Purpose='HTTP smoke test' }
    $result = Http $borrow '/borrower/reservation' $reservationForm
    $reservationId = [Guid](Sql 'SELECT Id FROM Reservations WHERE RequestId=@id' @{id=[Guid]$reservationForm.RequestId})
    Check ($result.Status -eq 302 -and $result.Location -match 'state=submitted') 'Borrower request persisted'
    $conflictForm = $reservationForm.Clone(); $conflictForm.RequestId=[Guid]::NewGuid()
    $result = Http $borrow '/borrower/reservation' $conflictForm
    Check ($result.Status -eq 200 -and $result.Body -match 'Reservation conflict') 'Overlap feedback'
    $details = "/custodian/reservations/$reservationId"
    $result = Post $staff "$details/approve" @{} $details
    Check ((Sql 'SELECT Status FROM Reservations WHERE Id=@id' @{id=$reservationId}) -eq 'Approved') 'Approval persisted'
    $result = Post $staff "$details/release" @{condition='Good'} $details
    Check ((Sql 'SELECT Status FROM Reservations WHERE Id=@id' @{id=$reservationId}) -eq 'Approved') 'Early release rejected'
    # Only this isolated fixture booking is moved into its release window for the HTTP transition check.
    [void](Sql 'UPDATE Reservations SET StartAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=@id' @{id=$reservationId})
    $result = Post $staff "$details/release" @{condition='Excellent';notes='Inspected'} $details
    Check ((Sql 'SELECT Status FROM Reservations WHERE Id=@id' @{id=$reservationId}) -eq 'Released') 'Release persisted'
    $result = Post $staff "$details/return" @{condition='999'} $details
    Check ((Sql 'SELECT Status FROM Reservations WHERE Id=@id' @{id=$reservationId}) -eq 'Released') 'Invalid return condition rejected'
    $result = Post $staff "$details/return" @{condition='Damaged';notes='Test damage'} $details
    Check ((Sql 'SELECT Status FROM Reservations WHERE Id=@id' @{id=$reservationId}) -eq 'Completed') 'Return persisted'
    Check ([bool](Sql 'SELECT IsMaintenanceHold FROM EquipmentItems WHERE Id=@id' @{id=$itemId})) 'Damaged return opens maintenance hold'
    $caseId = [Guid](Sql 'SELECT Id FROM MaintenanceCases WHERE EquipmentItemId=@id' @{id=$itemId})
    $version = [Convert]::ToBase64String([byte[]](Sql 'SELECT RowVersion FROM MaintenanceCases WHERE Id=@id' @{id=$caseId}))
    $result = Post $staff "/admin/maintenance/$caseId/update" @{RowVersion=$version;Status='Closed';Condition='Good';Resolution='Test repair completed'} '/admin/maintenance'
    Check (!(Sql 'SELECT IsMaintenanceHold FROM EquipmentItems WHERE Id=@id' @{id=$itemId})) 'Maintenance repair restores availability'
    $userForm = UserForm $staff $borrower.Id 'Borrower' $false $true ('ST-' + $run)
    $result = Http $staff "/admin/users/$($borrower.Id)/edit" $userForm
    Check ($result.Status -eq 302 -and !(Sql 'SELECT IsEligible FROM BorrowerProfiles WHERE UserId=@id' @{id=$borrower.Id})) 'Pause borrower eligibility'
    $result = Http $borrow '/borrower/reservation' $conflictForm
    Check ($result.Body -match 'borrowing access is currently paused|Borrowing access is paused') 'Paused borrower blocked'
    $result = Http $staff "/admin/users/$($borrower.Id)/edit" $userForm
    Check ($result.Status -eq 200 -and $result.Body -match 'account changed') 'Stale user edit rejected'
    $userForm = UserForm $staff $borrower.Id 'Custodian' $true $true ('ST-' + $run)
    $result = Http $staff "/admin/users/$($borrower.Id)/edit" $userForm
    Check ($result.Status -eq 302) 'Verified custodian promotion'
    Check ((Http $borrow '/borrower/dashboard').Location -match '/auth/login') 'Role change revokes prior cookie immediately'
    $custodian = Login $borrower
    foreach ($route in @('/custodian/dashboard','/custodian/approvals','/custodian/returns','/custodian/history','/custodian/calendar')) { Check ((Http $custodian $route).Status -eq 200) "Custodian route $route" }
    $userForm = UserForm $staff $admin.Id 'Borrower' $true $false
    $result = Http $staff "/admin/users/$($admin.Id)/edit" $userForm
    Check ($result.Status -eq 200 -and $result.Body -match 'cannot deactivate your own') 'Own administrator access protected'
    $userForm = UserForm $staff $admin.Id 'Administrator' $true $true ('ST-' + $run)
    $result = Http $staff "/admin/users/$($admin.Id)/edit" $userForm
    Check ($result.Status -eq 200 -and $result.Body -match 'student number') 'Duplicate student number rejected'
    $userForm = UserForm $staff $borrower.Id 'Borrower' $true $true ('ST-' + $run)
    [void](Http $staff "/admin/users/$($borrower.Id)/edit" $userForm)
    $borrow = Login $borrower
    Check ([int](Sql 'SELECT COUNT(*) FROM BorrowerProfiles WHERE UserId=@id' @{id=$borrower.Id}) -eq 1) 'Role changes retain borrower profile'
    $requestId=[Guid]::NewGuid(); $start=[DateTime]::UtcNow.AddHours(9); $end=$start.AddHours(1)
    $result = Post $staff '/admin/reservations' @{'Input.RequestId'=$requestId;'Input.BorrowerUserId'=$borrower.Id;'Input.EquipmentItemId'=$itemId;'Input.StartLocal'=$start.ToString('yyyy-MM-ddTHH:mm');'Input.EndLocal'=$end.ToString('yyyy-MM-ddTHH:mm');'Input.Purpose'='Staff booking'}
    Check ($result.Status -eq 302) 'Administrator booking for borrower'
    $id=[Guid](Sql 'SELECT Id FROM Reservations WHERE RequestId=@id' @{id=$requestId})
    $result=Post $borrow "/borrower/reservation/$id/cancel" @{} '/borrower/history'
    Check ((Sql 'SELECT Status FROM Reservations WHERE Id=@id' @{id=$id}) -eq 'Cancelled') 'Borrower cancellation'
    Check ((Http $borrow '/borrower/reservation' @{EquipmentItemId=$itemId}).Status -eq 400) 'CSRF enforced'
    $reset=Client; $result=Post $reset '/auth/password-reset-request' @{Email=$borrower.Email}
    $result=Post $reset '/auth/password-reset-code' @{Code=(Code $borrower.Email)}
    Check ($result.Location -eq '/auth/password-reset-new') 'Reset code authorizes password form'
    [void](Sql 'UPDATE AspNetUsers SET SecurityStamp=NEWID(),ConcurrencyStamp=NEWID() WHERE Id=@id' @{id=$borrower.Id})
    Check ((Http $reset '/auth/password-reset-new').Location -eq '/auth/password-reset-request') 'Stale reset permission rejected'
    $pending=Client; $result=Post $pending '/auth/login' @{Email=$admin.Email;Password=$admin.Password}
    Check ($result.Location -eq '/auth/two-factor') 'New admin flow pending'
    [void](Sql 'UPDATE AspNetUsers SET SecurityStamp=NEWID(),ConcurrencyStamp=NEWID() WHERE Id=@id' @{id=$admin.Id})
    Check ((Http $pending '/auth/two-factor').Location -eq '/auth/login') 'Stale administrator password step rejected'
    $catalog=Get-Content -Raw "$PSScriptRoot\..\CampusGear\CampusGear\wwwroot\figma\catalog.json" | ConvertFrom-Json
    foreach ($frame in $catalog) {
        $result=Http $anonymous $frame.url
        Check ($result.Status -eq 200 -and $result.Body.Contains('data-node-id="' + $frame.id + '"')) "Figma frame $($frame.id)"
    }
    $checkAssetFiles=Get-ChildItem "$PSScriptRoot\..\CampusGear\CampusGear\wwwroot\figma\assets" -File
    foreach ($file in $checkAssetFiles) { Check ((Http $anonymous ('/figma/assets/' + $file.Name)).Status -eq 200) "Local asset $($file.Name)" }
    $checks | ConvertTo-Json | Set-Content "$PSScriptRoot\live-smoke-results.json"
    $successful = $true
    Write-Output "$($checks.Count) HTTP/data assertions passed."
    if ($KeepFixtures) {
        @{Admin=@{Id=$admin.Id;Email=$admin.Email;Password=$admin.Password};Borrower=@{Id=$borrower.Id;Email=$borrower.Email;Password=$borrower.Password};Items=@($fixtureItems);Categories=@($fixtureCategories)} | ConvertTo-Json -Depth 5 | Set-Content "$env:TEMP\campusgear-root-smoke.json"
        Write-Output 'Test fixtures retained for browser inspection; identifiers are in the temporary smoke file.'
    }
}
finally {
    if (!$KeepFixtures -or !$successful) {
        foreach ($item in $fixtureItems) {
            [void](Sql 'DELETE FROM MaintenanceCases WHERE EquipmentItemId=@id; DELETE FROM Loans WHERE ReservationId IN (SELECT Id FROM Reservations WHERE EquipmentItemId=@id); DELETE FROM Reservations WHERE EquipmentItemId=@id; DELETE FROM EquipmentItems WHERE Id=@id' @{id=$item})
        }
        foreach ($category in $fixtureCategories) { [void](Sql 'DELETE FROM EquipmentCategories WHERE Id=@id' @{id=$category}) }
        foreach ($id in $fixtureUsers) {
            [void](Sql 'DELETE FROM AuditEvents WHERE ActorUserId=@id OR EntityId=@id; DELETE FROM EmailChallenges WHERE UserId=@id; DELETE FROM BorrowerProfiles WHERE UserId=@id; DELETE FROM AspNetUserRoles WHERE UserId=@id; DELETE FROM AspNetUsers WHERE Id=@id' @{id=$id})
        }
    }
    $connection.Close()
}
