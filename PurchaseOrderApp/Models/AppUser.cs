namespace PurchaseOrderApp.Models;

public class AppUser
{
    public int AppUserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int AppRoleId { get; set; }

    public AppRole Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public DateTime? PasswordUpdatedAt { get; set; }

    public string? SignatureFileName { get; set; }

    public byte[]? SignatureContent { get; set; }
}
