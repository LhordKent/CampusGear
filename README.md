# CampusGear
## _Campus equipment borrowing system_

Built on the ASI C# BaseCode structure: .NET 8, ASP.NET Core MVC + Identity, EF Core, and SQL Server.

## Installation
1.  Open the `ASI.Basecode.sln` file.
    _※Use Visual Studio 2022_
2.  Change the `CampusGear` connection string in `ASI.Basecode.WebApp/appsettings.json` based on your server.
    _Development defaults to `.\SQLEXPRESS` / database `CampusGear` with Windows authentication._
3.  Set `ASI.Basecode.WebApp` as the Startup project.
4.  Clean and ReBuild the solution.
5.  Run the `http` launch profile and open `http://localhost:5264/auth/login`.
    _Starting in Development applies existing migrations and seeds basic equipment._

From the command line:

```powershell
dotnet restore .\ASI.Basecode.sln
dotnet run --project .\ASI.Basecode.WebApp --launch-profile http
```

## Initial administrator

There is no committed default password. This creates the first administrator only when none exists:

```powershell
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_EMAIL = 'your-email@example.com'
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_NAME = 'Your Name'
$setupPassword = Read-Host 'Initial administrator password' -AsSecureString
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $setupPassword).Password
dotnet run --project .\ASI.Basecode.WebApp --launch-profile http -- --initialize-only
Remove-Item Env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD
```

New borrowers register at `/auth/signup` and wait for approval at `/admin/users?status=pending`.

## Email

SMTP is the default. Set `Email:Smtp:Host`, `From`, `Port` (587), `EnableSsl` (true), `Username`, and `Password` in User Secrets:

```powershell
dotnet user-secrets set "Email:Smtp:Host" "smtp.gmail.com" --project .\ASI.Basecode.WebApp
```

For local testing without real mail, use the in-memory mailbox (Development only) and view it at `/auth/development-inbox`:

```powershell
dotnet user-secrets set "Email:Provider" "Development" --project .\ASI.Basecode.WebApp
```

## Code Structure

- `ASI.Basecode.Data`
    - This project contains the repositories, entities, migrations and other logic that involves the database.
    - Make sure that the repository is for db processing only.
    - If it involves additional logic, move the logic into the service file.
- `ASI.Basecode.Resources`
    - This project is dedicated to storing messages, labels, roles, enums and other resources used by the website.
    - It serves as a centralized location for managing static content that is displayed to users.
- `ASI.Basecode.Services`
    - This project contains the services (business rules and workflows) that run before connecting to the repository.
    - This can also contain classes that can be used by the WebApp project with/without db processing.
- `ASI.Basecode.WebApp`
    - This project contains the main code, especially the `Controllers` and `Views`.
    - Make sure that the controllers are clean and no other logic is involved.
    - If it involves additional logic, move the logic into the service file.
