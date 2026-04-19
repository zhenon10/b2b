using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2B.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApprovedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                schema: "app",
                table: "Users",
                type: "datetime2",
                nullable: true);

            // Existing accounts remain able to sign in; new self-registrations start with null until approved.
            migrationBuilder.Sql(
                "UPDATE app.Users SET ApprovedAtUtc = CreatedAtUtc WHERE ApprovedAtUtc IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                schema: "app",
                table: "Users");
        }
    }
}
