using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AbfuhrPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProblemPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemPhotos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemPhotos_CompanyId",
                table: "ProblemPhotos",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemPhotos_ProblemReportId",
                table: "ProblemPhotos",
                column: "ProblemReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemPhotos");
        }
    }
}
