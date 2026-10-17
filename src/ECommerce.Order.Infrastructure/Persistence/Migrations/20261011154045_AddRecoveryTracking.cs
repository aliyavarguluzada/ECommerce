using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc/>
    public partial class AddRecoveryTracking : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRecoveryAttemptAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LastRecoveryAttemptAt", table: "orders");

        }

    }

}
