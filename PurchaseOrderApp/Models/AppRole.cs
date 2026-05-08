namespace PurchaseOrderApp.Models;

public class AppRole
{
    public int AppRoleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool CanAccessPurchaseOrders { get; set; }

    public bool CanAccessJobCards { get; set; }

    public bool CanAccessWialonUnits { get; set; }

    public bool CanAccessTrackingCertificates { get; set; }

    public bool CanAccessInventory { get; set; }

    public bool CanAccessConnectivitySettings { get; set; }

    public bool CanManageUsers { get; set; }

    public ICollection<AppUser> Users { get; set; } = [];
}
