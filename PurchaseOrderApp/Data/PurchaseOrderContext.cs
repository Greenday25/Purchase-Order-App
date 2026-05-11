using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PurchaseOrderApp.Models;
using System.Data;
using System.Text.Json;

namespace PurchaseOrderApp.Data
{
    public class PurchaseOrderContext : DbContext
    {
        private const string InitialMigrationId = "20260510170633_InitialCreate";
        private const string InvoiceUploaderMetadataMigrationId = "20260510194029_InvoiceUploaderMetadata";
        private const string PurchaseOrderUpdatedAtMigrationId = "20260510224500_AddPurchaseOrderUpdatedAt";
        private const string EfProductVersion = "10.0.5";

        public PurchaseOrderContext()
        {
        }

        public PurchaseOrderContext(DbContextOptions<PurchaseOrderContext> options)
            : base(options)
        {
        }

        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<JobCardRecord> JobCards { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<InventoryTrackingUnit> InventoryTrackingUnits { get; set; }
        public DbSet<InventoryReceipt> InventoryReceipts { get; set; }
        public DbSet<InventoryReceiptLine> InventoryReceiptLines { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<AppRole> AppRoles { get; set; }

        public void MigrateSafely()
        {
            if (Database.IsSqlite())
            {
                StampLegacySqliteMigrationsIfNeeded();
            }

            Database.Migrate();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }

            var databaseSettings = LoadDatabaseSettings();
            if (string.Equals(databaseSettings.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder
                    .UseSqlServer(databaseSettings.ConnectionString)
                    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
                return;
            }

            optionsBuilder
                .UseSqlite(ResolveSqliteConnectionString(databaseSettings.ConnectionString))
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        private static DatabaseSettings LoadDatabaseSettings()
        {
            var settingsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!System.IO.File.Exists(settingsPath))
            {
                return DatabaseSettings.Default;
            }

            try
            {
                using var document = JsonDocument.Parse(System.IO.File.ReadAllText(settingsPath));
                if (!document.RootElement.TryGetProperty("Database", out var databaseElement))
                {
                    return DatabaseSettings.Default;
                }

                var provider = databaseElement.TryGetProperty("Provider", out var providerElement)
                    ? providerElement.GetString()
                    : null;
                var connectionString = databaseElement.TryGetProperty("ConnectionString", out var connectionStringElement)
                    ? connectionStringElement.GetString()
                    : null;

                return new DatabaseSettings(
                    string.IsNullOrWhiteSpace(provider) ? DatabaseSettings.Default.Provider : provider,
                    string.IsNullOrWhiteSpace(connectionString) ? DatabaseSettings.Default.ConnectionString : connectionString);
            }
            catch
            {
                return DatabaseSettings.Default;
            }
        }

        private static string ResolveSqliteConnectionString(string connectionString)
        {
            const string dataSourcePrefix = "Data Source=";
            if (!connectionString.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return connectionString;
            }

            var databasePath = connectionString[dataSourcePrefix.Length..].Trim();
            if (System.IO.Path.IsPathRooted(databasePath))
            {
                return connectionString;
            }

            return $"{dataSourcePrefix}{System.IO.Path.Combine(AppContext.BaseDirectory, databasePath)}";
        }

        private void StampLegacySqliteMigrationsIfNeeded()
        {
            var connection = Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                if (!SqliteTableExists(connection, "AppRoles"))
                {
                    return;
                }

                EnsureLegacySqliteSchema(connection);
                EnsureMigrationHistoryTable(connection);
                InsertMigrationHistoryRow(connection, InitialMigrationId);

                if (SqliteTableHasColumns(
                    connection,
                    "PurchaseOrders",
                    "InvoiceUploadedAt",
                    "InvoiceUploadedByAppUserId",
                    "InvoiceUploadedByDisplayName"))
                {
                    InsertMigrationHistoryRow(connection, InvoiceUploaderMetadataMigrationId);
                }

                if (SqliteTableHasColumns(connection, "PurchaseOrders", "UpdatedAt"))
                {
                    InsertMigrationHistoryRow(connection, PurchaseOrderUpdatedAtMigrationId);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static void EnsureLegacySqliteSchema(System.Data.Common.DbConnection connection)
        {
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanAccessPurchaseOrders", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanManagerApprovePurchaseOrders", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanApprovePurchaseOrders", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanAccessJobCards", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanAccessWialonUnits", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanAccessTrackingCertificates", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanAccessInventory", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanAccessConnectivitySettings", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "AppRoles", "CanManageUsers", "INTEGER NOT NULL DEFAULT 0");

            AddSqliteColumnIfMissing(connection, "AppUsers", "PasswordHash", "TEXT NOT NULL DEFAULT ''");
            AddSqliteColumnIfMissing(connection, "AppUsers", "PasswordSalt", "TEXT NOT NULL DEFAULT ''");
            AddSqliteColumnIfMissing(connection, "AppUsers", "PasswordUpdatedAt", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "AppUsers", "SignatureFileName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "AppUsers", "SignatureContent", "BLOB NULL");

            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "OrderNumberManuallyEdited", "INTEGER NOT NULL DEFAULT 0");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "CreatedByAppUserId", "INTEGER NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "CreatedByDisplayName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "AssignedManagerAppUserId", "INTEGER NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "AssignedManagerDisplayName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "UpdatedAt", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "IncludeVat", "INTEGER NOT NULL DEFAULT 1");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "VATPercent", "TEXT NOT NULL DEFAULT '15.0'");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "ManagerApprovedAt", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "ManagerApprovedByAppUserId", "INTEGER NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "ManagerApprovedByDisplayName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "DirectorApprovedAt", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "DirectorApprovedByAppUserId", "INTEGER NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "DirectorApprovedByDisplayName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "SupplierCopySentAt", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "RejectedAt", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "SignedOrderFileName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "SignedOrderContent", "BLOB NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "InvoiceFileName", "TEXT NULL");
            AddSqliteColumnIfMissing(connection, "PurchaseOrders", "InvoiceContent", "BLOB NULL");
        }

        private static void AddSqliteColumnIfMissing(
            System.Data.Common.DbConnection connection,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            if (!SqliteTableExists(connection, tableName) ||
                SqliteTableHasColumns(connection, tableName, columnName))
            {
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE \"{EscapeSqliteIdentifier(tableName)}\" ADD COLUMN \"{EscapeSqliteIdentifier(columnName)}\" {columnDefinition};";
            command.ExecuteNonQuery();
        }

        private static bool SqliteTableExists(System.Data.Common.DbConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
            var tableNameParameter = command.CreateParameter();
            tableNameParameter.ParameterName = "$tableName";
            tableNameParameter.Value = tableName;
            command.Parameters.Add(tableNameParameter);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static void EnsureMigrationHistoryTable(System.Data.Common.DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        private static void InsertMigrationHistoryRow(System.Data.Common.DbConnection connection, string migrationId)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($migrationId, $productVersion);
                """;

            var migrationIdParameter = command.CreateParameter();
            migrationIdParameter.ParameterName = "$migrationId";
            migrationIdParameter.Value = migrationId;
            command.Parameters.Add(migrationIdParameter);

            var productVersionParameter = command.CreateParameter();
            productVersionParameter.ParameterName = "$productVersion";
            productVersionParameter.Value = EfProductVersion;
            command.Parameters.Add(productVersionParameter);

            command.ExecuteNonQuery();
        }

        private static bool SqliteTableHasColumns(
            System.Data.Common.DbConnection connection,
            string tableName,
            params string[] columnNames)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{EscapeSqliteIdentifier(tableName)}\");";
            using var reader = command.ExecuteReader();
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }

            return columnNames.All(existingColumns.Contains);
        }

        private static string EscapeSqliteIdentifier(string identifier)
        {
            return identifier.Replace("\"", "\"\"");
        }

        private sealed record DatabaseSettings(string Provider, string ConnectionString)
        {
            public static DatabaseSettings Default { get; } = new("Sqlite", "Data Source=purchaseorders.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PurchaseOrder>()
                .HasMany(po => po.Lines)
                .WithOne(line => line.PurchaseOrder)
                .HasForeignKey(line => line.PurchaseOrderId);

            modelBuilder.Entity<PurchaseOrder>().HasOne(po => po.Vendor).WithMany(v => v.PurchaseOrders).HasForeignKey(po => po.VendorId);
            modelBuilder.Entity<JobCardRecord>().HasIndex(record => record.SequenceNumber).IsUnique();
            modelBuilder.Entity<JobCardRecord>().HasIndex(record => record.JobCardNumber).IsUnique();
            modelBuilder.Entity<InventoryItem>().HasIndex(item => item.ItemCode).IsUnique();
            modelBuilder.Entity<InventoryItem>()
                .HasMany(item => item.Transactions)
                .WithOne(transaction => transaction.InventoryItem)
                .HasForeignKey(transaction => transaction.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryTransaction>().HasIndex(transaction => transaction.JobCardRecordId);
            modelBuilder.Entity<InventoryTransaction>().HasIndex(transaction => transaction.IssueOutNumber).IsUnique();
            modelBuilder.Entity<InventoryTrackingUnit>().HasIndex(unit => unit.SerialNumber).IsUnique();
            modelBuilder.Entity<InventoryTrackingUnit>().HasIndex(unit => unit.ImeiNumber).IsUnique();
            modelBuilder.Entity<InventoryTrackingUnit>().HasIndex(unit => unit.InventoryItemId);
            modelBuilder.Entity<InventoryTrackingUnit>().HasIndex(unit => unit.IsIssued);
            modelBuilder.Entity<InventoryTrackingUnit>()
                .HasOne(unit => unit.InventoryItem)
                .WithMany(item => item.TrackingUnits)
                .HasForeignKey(unit => unit.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryReceipt>().HasIndex(receipt => receipt.ReceiptNumber).IsUnique();
            modelBuilder.Entity<InventoryReceipt>()
                .HasMany(receipt => receipt.Lines)
                .WithOne(line => line.InventoryReceipt)
                .HasForeignKey(line => line.InventoryReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryReceiptLine>().HasIndex(line => line.ReceiptNumber);
            modelBuilder.Entity<InventoryReceiptLine>().HasIndex(line => line.InventoryItemId);
            modelBuilder.Entity<InventoryReceiptLine>().HasIndex(line => line.InventoryTransactionId);
            modelBuilder.Entity<InventoryReceiptLine>().HasIndex(line => line.PurchaseOrderId);
            modelBuilder.Entity<InventoryReceiptLine>()
                .HasOne(line => line.InventoryItem)
                .WithMany()
                .HasForeignKey(line => line.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryReceiptLine>()
                .HasOne(line => line.InventoryTransaction)
                .WithMany()
                .HasForeignKey(line => line.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AppRole>().HasIndex(role => role.Name).IsUnique();
            modelBuilder.Entity<AppUser>().HasIndex(user => user.DisplayName).IsUnique();
            modelBuilder.Entity<AppUser>()
                .HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.AppRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
