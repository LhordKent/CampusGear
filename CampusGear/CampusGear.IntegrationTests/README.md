# SQL Server reservation integration tests

Run `dotnet test CampusGear.IntegrationTests.csproj` with SQL Server Express available.
The test fixture creates a uniquely named `CampusGear_Integration_<guid>` database,
applies the application's EF migrations, inserts test-only users/equipment, and deletes
that database after the run. It never inserts fixtures into the live CampusGear database.

The default SQL connection uses `.\SQLEXPRESS` with Windows authentication. To use another
SQL Server, set `CAMPUSGEAR_TEST_SQLSERVER` to a base connection string. Its database name
is always replaced with the generated test database name. The account needs permission
to create and drop that database.

The tests cover concurrent overlaps, concurrent duplicate submissions, half-open adjacent
slots, pending hold expiry, malformed holds, authorization, eligibility changes while
waiting on a borrower lock, release, late/damaged return, maintenance holds, and an
outstanding loan blocking a later equipment release.
