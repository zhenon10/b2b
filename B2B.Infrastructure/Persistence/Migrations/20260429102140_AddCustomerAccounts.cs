using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2B.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerAccounts",
                schema: "app",
                columns: table => new
                {
                    CustomerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccounts", x => x.CustomerAccountId);
                    table.CheckConstraint("CK_CustomerAccounts_Balance_NonNegative", "Balance >= 0");
                    table.ForeignKey(
                        name: "FK_CustomerAccounts_Users_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_CustomerAccounts_Users_SellerUserId",
                        column: x => x.SellerUserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccountEntries",
                schema: "app",
                columns: table => new
                {
                    CustomerAccountEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountEntries", x => x.CustomerAccountEntryId);
                    table.CheckConstraint("CK_CustomerAccountEntries_Amount_Positive", "Amount > 0");
                    table.ForeignKey(
                        name: "FK_CustomerAccountEntries_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalSchema: "app",
                        principalTable: "CustomerAccounts",
                        principalColumn: "CustomerAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_Account_CreatedAt",
                schema: "app",
                table: "CustomerAccountEntries",
                columns: new[] { "CustomerAccountId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_CustomerAccountEntries_Order_Type",
                schema: "app",
                table: "CustomerAccountEntries",
                columns: new[] { "OrderId", "Type" },
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccounts_Seller_Currency_Balance",
                schema: "app",
                table: "CustomerAccounts",
                columns: new[] { "SellerUserId", "CurrencyCode", "Balance" });

            migrationBuilder.CreateIndex(
                name: "UX_CustomerAccounts_Buyer_Seller_Currency",
                schema: "app",
                table: "CustomerAccounts",
                columns: new[] { "BuyerUserId", "SellerUserId", "CurrencyCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAccountEntries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "CustomerAccounts",
                schema: "app");
        }
    }
}
