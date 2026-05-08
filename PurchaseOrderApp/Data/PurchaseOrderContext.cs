using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Models;

namespace PurchaseOrderApp.Data
{
    public class PurchaseOrderContext : DbContext
    {
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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var databasePath = System.IO.Path.Combine(AppContext.BaseDirectory, "purchaseorders.db");
            optionsBuilder.UseSqlite($"Data Source={databasePath}");
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
