using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc/>
    public partial class InitialOrder : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(name: "orders", columns: table => new {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestFingerprint = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                ShippingAddress_Recipient = table.Column<string>(
                    type: "character varying(150)",
                    maxLength: 150,
                    nullable: false),
                ShippingAddress_Line1 = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false),
                ShippingAddress_Line2 = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true),
                ShippingAddress_City = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                ShippingAddress_Region = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true),
                ShippingAddress_PostalCode = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: false),
                ShippingAddress_CountryCode = table.Column<string>(
                    type: "character varying(2)",
                    maxLength: 2,
                    nullable: false),
                Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                CancellationReason = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true),
                Carrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                TrackingNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.Id);

            });

            migrationBuilder.CreateTable(name: "order_items", columns: table => new {
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(
                    type: "numeric(18,2)",
                    precision: 18,
                    scale: 2,
                    nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_order_items", x => new { x.OrderId, x.ProductId });

                table.ForeignKey(
                    name: "FK_order_items_orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);

            });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId_CreatedAt",
                table: "orders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(name: "IX_orders_Status", table: "orders", column: "Status");

        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "order_items");

            migrationBuilder.DropTable(name: "orders");

        }

    }

}
