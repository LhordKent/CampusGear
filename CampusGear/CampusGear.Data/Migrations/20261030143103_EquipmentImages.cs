using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusGear.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "EquipmentItems",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "EquipmentItems");
        }
    }
}
