using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurchaseOrderApp.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceUploaderMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceUploadedAt",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceUploadedByAppUserId",
                table: "PurchaseOrders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceUploadedByDisplayName",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceUploadedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceUploadedByAppUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceUploadedByDisplayName",
                table: "PurchaseOrders");
        }
    }
}
