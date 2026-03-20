using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Models;

namespace PurchaseOrderApp.Data
{
    public class PurchaseOrderContext : DbContext
    {
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=purchaseorders.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PurchaseOrder>()
                .HasMany(po => po.Lines)
                .WithOne(line => line.PurchaseOrder)
                .HasForeignKey(line => line.PurchaseOrderId);

            modelBuilder.Entity<PurchaseOrder>().HasOne(po => po.Vendor).WithMany(v => v.PurchaseOrders).HasForeignKey(po => po.VendorId);
        }
    }
}
