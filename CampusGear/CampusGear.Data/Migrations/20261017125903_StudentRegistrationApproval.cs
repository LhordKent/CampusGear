using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusGear.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentRegistrationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Existing accounts: the last word becomes the last name and everything before it the first name.
            // Single-word names are kept as the first name with an empty last name.
            migrationBuilder.Sql("""
                UPDATE u
                SET FirstName = LEFT(CASE WHEN p.LastSpace = 0 THEN t.Name ELSE RTRIM(LEFT(t.Name, LEN(t.Name) - p.LastSpace)) END, 100),
                    LastName = LEFT(CASE WHEN p.LastSpace = 0 THEN N'' ELSE RIGHT(t.Name, p.LastSpace - 1) END, 100)
                FROM AspNetUsers u
                CROSS APPLY (SELECT LTRIM(RTRIM(u.FullName)) AS Name) t
                CROSS APPLY (SELECT CHARINDEX(N' ', REVERSE(t.Name)) AS LastSpace) p;
                """);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(201)",
                maxLength: 201,
                nullable: false,
                computedColumnSql: "LTRIM(RTRIM([FirstName] + N' ' + [LastName]))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "AspNetUsers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Approved");

            // Accounts that never finished the retired email verification now wait for administrator approval.
            migrationBuilder.Sql("UPDATE AspNetUsers SET ApprovalStatus = N'PendingApproval' WHERE EmailConfirmed = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE AspNetUsers SET FullName = LEFT(LTRIM(RTRIM(FirstName + N' ' + LastName)), 160);");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AspNetUsers");
        }
    }
}
