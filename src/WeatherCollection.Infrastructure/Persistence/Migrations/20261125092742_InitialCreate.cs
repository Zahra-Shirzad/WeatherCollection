using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherCollection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeatherLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeatherRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimVersion = table.Column<long>(type: "bigint", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherLocations_WeatherRequests_WeatherRequestId",
                        column: x => x.WeatherRequestId,
                        principalTable: "WeatherRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeatherResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeatherLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    RelativeHumidity = table.Column<double>(type: "float", nullable: false),
                    WindSpeed = table.Column<double>(type: "float", nullable: false),
                    WeatherTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherResults_WeatherLocations_WeatherLocationId",
                        column: x => x.WeatherLocationId,
                        principalTable: "WeatherLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherLocations_Status_NextAttemptAtUtc_LeaseUntilUtc",
                table: "WeatherLocations",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherLocations_WeatherRequestId_Sequence",
                table: "WeatherLocations",
                columns: new[] { "WeatherRequestId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherRequests_RequestId",
                table: "WeatherRequests",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherResults_WeatherLocationId",
                table: "WeatherResults",
                column: "WeatherLocationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherResults");

            migrationBuilder.DropTable(
                name: "WeatherLocations");

            migrationBuilder.DropTable(
                name: "WeatherRequests");
        }
    }
}
