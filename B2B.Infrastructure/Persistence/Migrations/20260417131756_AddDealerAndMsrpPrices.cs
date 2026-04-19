using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2B.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerAndMsrpPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Name_Search",
                schema: "app",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerUserId_IsActive",
                schema: "app",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Price_NonNegative",
                schema: "app",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "Price",
                schema: "app",
                table: "Products",
                newName: "MsrpPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "DealerPrice",
                schema: "app",
                table: "Products",
                type: "decimal(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name_Search",
                schema: "app",
                table: "Products",
                column: "Name")
                .Annotation("SqlServer:Include", new[] { "SellerUserId", "DealerPrice", "MsrpPrice", "CurrencyCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerUserId_IsActive",
                schema: "app",
                table: "Products",
                columns: new[] { "SellerUserId", "IsActive" })
                .Annotation("SqlServer:Include", new[] { "Name", "DealerPrice", "MsrpPrice", "CurrencyCode", "StockQuantity" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_DealerPrice_NonNegative",
                schema: "app",
                table: "Products",
                sql: "DealerPrice >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_MsrpPrice_NonNegative",
                schema: "app",
                table: "Products",
                sql: "MsrpPrice >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Name_Search",
                schema: "app",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerUserId_IsActive",
                schema: "app",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_DealerPrice_NonNegative",
                schema: "app",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_MsrpPrice_NonNegative",
                schema: "app",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DealerPrice",
                schema: "app",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "MsrpPrice",
                schema: "app",
                table: "Products",
                newName: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name_Search",
                schema: "app",
                table: "Products",
                column: "Name")
                .Annotation("SqlServer:Include", new[] { "SellerUserId", "Price", "CurrencyCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerUserId_IsActive",
                schema: "app",
                table: "Products",
                columns: new[] { "SellerUserId", "IsActive" })
                .Annotation("SqlServer:Include", new[] { "Name", "Price", "CurrencyCode", "StockQuantity" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Price_NonNegative",
                schema: "app",
                table: "Products",
                sql: "Price >= 0");
        }
    }
}
