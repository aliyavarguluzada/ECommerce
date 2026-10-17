using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc/>
    public partial class AddOrderPayment : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "PaymentId", table: "orders", type: "uuid", nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentRefunded",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaymentId", table: "orders");

            migrationBuilder.DropColumn(name: "PaymentRefunded", table: "orders");

        }

    }

}
