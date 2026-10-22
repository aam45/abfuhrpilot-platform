using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AbfuhrPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LengthMeters",
                table: "Vehicles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxLoadTons",
                table: "Vehicles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WidthMeters",
                table: "Vehicles",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LengthMeters",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "MaxLoadTons",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "WidthMeters",
                table: "Vehicles");
        }
    }
}
