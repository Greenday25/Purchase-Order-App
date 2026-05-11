using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurchaseOrderApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRoles",
                columns: table => new
                {
                    AppRoleId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CanAccessPurchaseOrders = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanManagerApprovePurchaseOrders = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanApprovePurchaseOrders = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanAccessJobCards = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanAccessWialonUnits = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanAccessTrackingCertificates = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanAccessInventory = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanAccessConnectivitySettings = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanManageUsers = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRoles", x => x.AppRoleId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    InventoryItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemCode = table.Column<string>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsTrackingUnit = table.Column<bool>(type: "INTEGER", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.InventoryItemId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReceipts",
                columns: table => new
                {
                    InventoryReceiptId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReceipts", x => x.InventoryReceiptId);
                });

            migrationBuilder.CreateTable(
                name: "JobCards",
                columns: table => new
                {
                    JobCardRecordId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    JobCardNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WorkflowStatus = table.Column<string>(type: "TEXT", nullable: false),
                    JobCardType = table.Column<string>(type: "TEXT", nullable: false),
                    StatusNotes = table.Column<string>(type: "TEXT", nullable: true),
                    DetailsConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAmendedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AmendmentNotes = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasVehiclePhoto = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasRegistrationPhoto = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasVinPhoto = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTrackingUnitPhoto = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseCustomBillingSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CustomBillingSystemName = table.Column<string>(type: "TEXT", nullable: true),
                    SystemPriceExVat = table.Column<string>(type: "TEXT", nullable: true),
                    HasPanicButton = table.Column<bool>(type: "INTEGER", nullable: false),
                    PanicButtonPriceExVat = table.Column<string>(type: "TEXT", nullable: true),
                    HasEarlyWarningSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    EarlyWarningSystemPriceExVat = table.Column<string>(type: "TEXT", nullable: true),
                    BleSensorQuantity = table.Column<string>(type: "TEXT", nullable: true),
                    BleSensorUnitPriceExVat = table.Column<string>(type: "TEXT", nullable: true),
                    HasLvCanAdaptor = table.Column<bool>(type: "INTEGER", nullable: false),
                    LvCanAdaptorPriceExVat = table.Column<string>(type: "TEXT", nullable: true),
                    OtherHardwareDescription = table.Column<string>(type: "TEXT", nullable: true),
                    OtherHardwarePriceExVat = table.Column<string>(type: "TEXT", nullable: true),
                    BillingNotes = table.Column<string>(type: "TEXT", nullable: true),
                    WialonUnitId = table.Column<long>(type: "INTEGER", nullable: true),
                    WialonUnitName = table.Column<string>(type: "TEXT", nullable: true),
                    WialonAccountId = table.Column<long>(type: "INTEGER", nullable: true),
                    WialonAccountName = table.Column<string>(type: "TEXT", nullable: true),
                    WialonCreatorId = table.Column<long>(type: "INTEGER", nullable: true),
                    WialonCreatorName = table.Column<string>(type: "TEXT", nullable: true),
                    WialonHardwareTypeId = table.Column<long>(type: "INTEGER", nullable: true),
                    WialonHardwareTypeName = table.Column<string>(type: "TEXT", nullable: true),
                    JobCardName = table.Column<string>(type: "TEXT", nullable: false),
                    UniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    Iccid = table.Column<string>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Brand = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    Colour = table.Column<string>(type: "TEXT", nullable: false),
                    VehicleClass = table.Column<string>(type: "TEXT", nullable: false),
                    VehicleType = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationPlate = table.Column<string>(type: "TEXT", nullable: false),
                    Vin = table.Column<string>(type: "TEXT", nullable: false),
                    Client = table.Column<string>(type: "TEXT", nullable: false),
                    Contact1 = table.Column<string>(type: "TEXT", nullable: false),
                    Contact2 = table.Column<string>(type: "TEXT", nullable: false),
                    MakeAndModel = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationFleet = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobCards", x => x.JobCardRecordId);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    VendorId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.VendorId);
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    AppRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.AppUserId);
                    table.ForeignKey(
                        name: "FK_AppUsers_AppRoles_AppRoleId",
                        column: x => x.AppRoleId,
                        principalTable: "AppRoles",
                        principalColumn: "AppRoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    InventoryTransactionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityAfterTransaction = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IssueOutNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ItemCodeSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CategorySnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    IsTrackingUnit = table.Column<bool>(type: "INTEGER", nullable: false),
                    JobCardRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobCardNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InventoryTransactionId);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderNumber = table.Column<string>(type: "TEXT", nullable: false),
                    OrderNumberManuallyEdited = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByAppUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedByDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedManagerAppUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedManagerDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", nullable: false),
                    VendorId = table.Column<int>(type: "INTEGER", nullable: false),
                    BillTo = table.Column<string>(type: "TEXT", nullable: false),
                    BillToAddress = table.Column<string>(type: "TEXT", nullable: false),
                    IncludeVat = table.Column<bool>(type: "INTEGER", nullable: false),
                    VATPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    ManagerApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DirectorApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SupplierCopySentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedOrderFileName = table.Column<string>(type: "TEXT", nullable: true),
                    SignedOrderContent = table.Column<byte[]>(type: "BLOB", nullable: true),
                    InvoiceFileName = table.Column<string>(type: "TEXT", nullable: true),
                    InvoiceContent = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.PurchaseOrderId);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "VendorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReceiptLines",
                columns: table => new
                {
                    InventoryReceiptLineId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "TEXT", nullable: false),
                    InventoryItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    InventoryTransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierName = table.Column<string>(type: "TEXT", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "TEXT", nullable: true),
                    QuantityReceived = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemCodeSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CategorySnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    IsTrackingUnit = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReceiptLines", x => x.InventoryReceiptLineId);
                    table.ForeignKey(
                        name: "FK_InventoryReceiptLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReceiptLines_InventoryReceipts_InventoryReceiptId",
                        column: x => x.InventoryReceiptId,
                        principalTable: "InventoryReceipts",
                        principalColumn: "InventoryReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReceiptLines_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTrackingUnits",
                columns: table => new
                {
                    InventoryTrackingUnitId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ImeiNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedInventoryTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsIssued = table.Column<bool>(type: "INTEGER", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IssuedInventoryTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssuedJobCardRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssuedJobCardNumber = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTrackingUnits", x => x.InventoryTrackingUnitId);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingUnits_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingUnits_InventoryTransactions_IssuedInventoryTransactionId",
                        column: x => x.IssuedInventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId");
                    table.ForeignKey(
                        name: "FK_InventoryTrackingUnits_InventoryTransactions_ReceivedInventoryTransactionId",
                        column: x => x.ReceivedInventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    PurchaseOrderLineId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PurchaseOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    PartNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.PurchaseOrderLineId);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRoles_Name",
                table: "AppRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_AppRoleId",
                table: "AppUsers",
                column: "AppRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_DisplayName",
                table: "AppUsers",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemCode",
                table: "InventoryItems",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReceiptLines_InventoryItemId",
                table: "InventoryReceiptLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReceiptLines_InventoryReceiptId",
                table: "InventoryReceiptLines",
                column: "InventoryReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReceiptLines_InventoryTransactionId",
                table: "InventoryReceiptLines",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReceiptLines_PurchaseOrderId",
                table: "InventoryReceiptLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReceiptLines_ReceiptNumber",
                table: "InventoryReceiptLines",
                column: "ReceiptNumber");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReceipts_ReceiptNumber",
                table: "InventoryReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingUnits_ImeiNumber",
                table: "InventoryTrackingUnits",
                column: "ImeiNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingUnits_InventoryItemId",
                table: "InventoryTrackingUnits",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingUnits_IsIssued",
                table: "InventoryTrackingUnits",
                column: "IsIssued");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingUnits_IssuedInventoryTransactionId",
                table: "InventoryTrackingUnits",
                column: "IssuedInventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingUnits_ReceivedInventoryTransactionId",
                table: "InventoryTrackingUnits",
                column: "ReceivedInventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingUnits_SerialNumber",
                table: "InventoryTrackingUnits",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryItemId",
                table: "InventoryTransactions",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_IssueOutNumber",
                table: "InventoryTransactions",
                column: "IssueOutNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_JobCardRecordId",
                table: "InventoryTransactions",
                column: "JobCardRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCards_JobCardNumber",
                table: "JobCards",
                column: "JobCardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobCards_SequenceNumber",
                table: "JobCards",
                column: "SequenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId",
                table: "PurchaseOrderLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_VendorId",
                table: "PurchaseOrders",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "InventoryReceiptLines");

            migrationBuilder.DropTable(
                name: "InventoryTrackingUnits");

            migrationBuilder.DropTable(
                name: "JobCards");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "AppRoles");

            migrationBuilder.DropTable(
                name: "InventoryReceipts");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Vendors");
        }
    }
}
