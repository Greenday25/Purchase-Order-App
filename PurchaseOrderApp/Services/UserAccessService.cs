using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Data;
using PurchaseOrderApp.Models;
using System.Security.Cryptography;

namespace PurchaseOrderApp.Services;

public sealed record UserPermissions(
    bool CanAccessPurchaseOrders,
    bool CanManagerApprovePurchaseOrders,
    bool CanApprovePurchaseOrders,
    bool CanAccessJobCards,
    bool CanAccessWialonUnits,
    bool CanAccessTrackingCertificates,
    bool CanAccessInventory,
    bool CanAccessConnectivitySettings,
    bool CanManageUsers);

public class UserAccessService
{
    private const string AdministratorRoleName = "Administrator";
    private const string DefaultInitialPassword = "admin";
    private const int PasswordSaltByteCount = 16;
    private const int PasswordHashByteCount = 32;
    private const int PasswordIterations = 120000;

    public IReadOnlyList<AppUser> GetActiveUsers()
    {
        using var db = CreateReadyContext();
        return db.AppUsers
            .Include(user => user.Role)
            .Where(user => user.IsActive)
            .OrderBy(user => user.DisplayName)
            .ToList();
    }

    public IReadOnlyList<AppUser> GetUsers()
    {
        using var db = CreateReadyContext();
        return db.AppUsers
            .Include(user => user.Role)
            .OrderByDescending(user => user.IsActive)
            .ThenBy(user => user.DisplayName)
            .ToList();
    }

    public IReadOnlyList<AppRole> GetRoles()
    {
        using var db = CreateReadyContext();
        return db.AppRoles
            .OrderBy(role => role.Name)
            .ToList();
    }

    public AppUser? AuthenticateUser(string displayName, string password)
    {
        using var db = CreateReadyContext();
        var normalizedName = NormalizeRequired(displayName, "User name");
        var user = db.AppUsers
            .Include(appUser => appUser.Role)
            .FirstOrDefault(appUser => appUser.IsActive && appUser.DisplayName.ToUpper() == normalizedName.ToUpper());

        if (user is null || !VerifyPassword(password, user.PasswordSalt, user.PasswordHash))
        {
            return null;
        }

        return user;
    }

    public AppUser CreateUser(string displayName, int appRoleId, string password)
    {
        using var db = CreateReadyContext();
        var normalizedName = NormalizeRequired(displayName, "User name");
        var normalizedPassword = NormalizeRequired(password, "Password");
        if (db.AppUsers.Any(user => user.DisplayName.ToUpper() == normalizedName.ToUpper()))
        {
            throw new InvalidOperationException($"A user named {normalizedName} already exists.");
        }

        if (!db.AppRoles.Any(role => role.AppRoleId == appRoleId))
        {
            throw new InvalidOperationException("Select a valid role before saving the user.");
        }

        var passwordParts = HashPassword(normalizedPassword);
        var user = new AppUser
        {
            DisplayName = normalizedName,
            AppRoleId = appRoleId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            PasswordHash = passwordParts.Hash,
            PasswordSalt = passwordParts.Salt,
            PasswordUpdatedAt = DateTime.Now
        };

        db.AppUsers.Add(user);
        db.SaveChanges();
        db.Entry(user).Reference(savedUser => savedUser.Role).Load();
        return user;
    }

    public void UpdateUser(int appUserId, string displayName, int appRoleId, bool isActive, string? newPassword)
    {
        using var db = CreateReadyContext();
        var user = db.AppUsers.Find(appUserId)
            ?? throw new InvalidOperationException("The selected user could not be found.");

        var normalizedName = NormalizeRequired(displayName, "User name");
        if (db.AppUsers.Any(existing => existing.AppUserId != appUserId && existing.DisplayName.ToUpper() == normalizedName.ToUpper()))
        {
            throw new InvalidOperationException($"A user named {normalizedName} already exists.");
        }

        if (!db.AppRoles.Any(role => role.AppRoleId == appRoleId))
        {
            throw new InvalidOperationException("Select a valid role before saving the user.");
        }

        if (!isActive && IsLastActiveAdministrator(db, user.AppUserId))
        {
            throw new InvalidOperationException("At least one active Administrator user must remain.");
        }

        user.DisplayName = normalizedName;
        user.AppRoleId = appRoleId;
        user.IsActive = isActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var passwordParts = HashPassword(newPassword.Trim());
            user.PasswordHash = passwordParts.Hash;
            user.PasswordSalt = passwordParts.Salt;
            user.PasswordUpdatedAt = DateTime.Now;
        }

        db.SaveChanges();
    }

    public void DeleteUser(int appUserId)
    {
        using var db = CreateReadyContext();
        var user = db.AppUsers.Find(appUserId)
            ?? throw new InvalidOperationException("The selected user could not be found.");

        if (IsLastActiveAdministrator(db, user.AppUserId))
        {
            throw new InvalidOperationException("At least one active Administrator user must remain.");
        }

        db.AppUsers.Remove(user);
        db.SaveChanges();
    }

    public AppRole CreateRole(AppRole input)
    {
        using var db = CreateReadyContext();
        var roleName = NormalizeRequired(input.Name, "Role name");
        if (db.AppRoles.Any(role => role.Name.ToUpper() == roleName.ToUpper()))
        {
            throw new InvalidOperationException($"A role named {roleName} already exists.");
        }

        var role = CopyRolePermissions(input);
        role.Name = roleName;
        db.AppRoles.Add(role);
        db.SaveChanges();
        return role;
    }

    public void UpdateRole(AppRole input)
    {
        using var db = CreateReadyContext();
        var role = db.AppRoles.Find(input.AppRoleId)
            ?? throw new InvalidOperationException("The selected role could not be found.");

        role.Name = NormalizeRequired(input.Name, "Role name");
        role.CanAccessPurchaseOrders = input.CanAccessPurchaseOrders;
        role.CanManagerApprovePurchaseOrders = input.CanManagerApprovePurchaseOrders;
        role.CanApprovePurchaseOrders = input.CanApprovePurchaseOrders;
        role.CanAccessJobCards = input.CanAccessJobCards;
        role.CanAccessWialonUnits = input.CanAccessWialonUnits;
        role.CanAccessTrackingCertificates = input.CanAccessTrackingCertificates;
        role.CanAccessInventory = input.CanAccessInventory;
        role.CanAccessConnectivitySettings = input.CanAccessConnectivitySettings;
        role.CanManageUsers = input.CanManageUsers;
        db.SaveChanges();
    }

    public UserPermissions GetPermissions(AppUser? user)
    {
        if (user?.Role is null)
        {
            return new UserPermissions(false, false, false, false, false, false, false, false, false);
        }

        return new UserPermissions(
            user.Role.CanAccessPurchaseOrders,
            user.Role.CanManagerApprovePurchaseOrders,
            user.Role.CanApprovePurchaseOrders,
            user.Role.CanAccessJobCards,
            user.Role.CanAccessWialonUnits,
            user.Role.CanAccessTrackingCertificates,
            user.Role.CanAccessInventory,
            user.Role.CanAccessConnectivitySettings,
            user.Role.CanManageUsers);
    }

    private static PurchaseOrderContext CreateReadyContext()
    {
        var db = new PurchaseOrderContext();
        db.Database.EnsureCreated();
        EnsureSchema(db);
        EnsureDefaults(db);
        return db;
    }

    private static void EnsureSchema(PurchaseOrderContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS AppRoles (
                AppRoleId INTEGER NOT NULL CONSTRAINT PK_AppRoles PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                CanAccessPurchaseOrders INTEGER NOT NULL DEFAULT 0,
                CanManagerApprovePurchaseOrders INTEGER NOT NULL DEFAULT 0,
                CanApprovePurchaseOrders INTEGER NOT NULL DEFAULT 0,
                CanAccessJobCards INTEGER NOT NULL DEFAULT 0,
                CanAccessWialonUnits INTEGER NOT NULL DEFAULT 0,
                CanAccessTrackingCertificates INTEGER NOT NULL DEFAULT 0,
                CanAccessInventory INTEGER NOT NULL DEFAULT 0,
                CanAccessConnectivitySettings INTEGER NOT NULL DEFAULT 0,
                CanManageUsers INTEGER NOT NULL DEFAULT 0
            );
            """);

        AddColumnIfMissing(db, "AppRoles", "CanManagerApprovePurchaseOrders", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "AppRoles", "CanApprovePurchaseOrders", "INTEGER NOT NULL DEFAULT 0");

        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_AppRoles_Name ON AppRoles (Name);
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS AppUsers (
                AppUserId INTEGER NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY AUTOINCREMENT,
                DisplayName TEXT NOT NULL,
                AppRoleId INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                PasswordHash TEXT NOT NULL DEFAULT '',
                PasswordSalt TEXT NOT NULL DEFAULT '',
                PasswordUpdatedAt TEXT NULL,
                CONSTRAINT FK_AppUsers_AppRoles_AppRoleId FOREIGN KEY (AppRoleId) REFERENCES AppRoles (AppRoleId) ON DELETE RESTRICT
            );
            """);

        AddColumnIfMissing(db, "AppUsers", "PasswordHash", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AppUsers", "PasswordSalt", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AppUsers", "PasswordUpdatedAt", "TEXT NULL");

        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_AppUsers_DisplayName ON AppUsers (DisplayName);
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS IX_AppUsers_AppRoleId ON AppUsers (AppRoleId);
            """);
    }

    private static void EnsureDefaults(PurchaseOrderContext db)
    {
        EnsureRole(db, AdministratorRoleName, true, true, true, true, true, true, true, true, true);
        EnsureRole(db, "Manager", true, true, false, false, false, false, false, false, false);
        EnsureRole(db, "Executive", true, false, true, false, false, false, false, false, false);
        EnsureRole(db, "Purchase Orders", true, false, false, false, false, false, false, false, false);
        EnsureRole(db, "Job Cards", false, false, false, true, true, true, false, false, false);
        EnsureRole(db, "Inventory", false, false, false, false, false, false, true, false, false);
        EnsureRole(db, "Operations", true, false, false, true, true, true, true, false, false);

        if (!db.AppUsers.Any())
        {
            var adminRole = db.AppRoles.Single(role => role.Name == AdministratorRoleName);
            var passwordParts = HashPassword(DefaultInitialPassword);
            db.AppUsers.Add(new AppUser
            {
                DisplayName = "Admin",
                AppRoleId = adminRole.AppRoleId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                PasswordHash = passwordParts.Hash,
                PasswordSalt = passwordParts.Salt,
                PasswordUpdatedAt = DateTime.Now
            });
            db.SaveChanges();
        }

        var usersWithoutPasswords = db.AppUsers
            .Where(user => user.PasswordHash == string.Empty || user.PasswordSalt == string.Empty)
            .ToList();
        if (usersWithoutPasswords.Count > 0)
        {
            foreach (var user in usersWithoutPasswords)
            {
                var passwordParts = HashPassword(DefaultInitialPassword);
                user.PasswordHash = passwordParts.Hash;
                user.PasswordSalt = passwordParts.Salt;
                user.PasswordUpdatedAt = DateTime.Now;
            }

            db.SaveChanges();
        }
    }

    private static void EnsureRole(
        PurchaseOrderContext db,
        string name,
        bool purchaseOrders,
        bool managerApprovePurchaseOrders,
        bool approvePurchaseOrders,
        bool jobCards,
        bool wialonUnits,
        bool trackingCertificates,
        bool inventory,
        bool connectivitySettings,
        bool manageUsers)
    {
        if (db.AppRoles.Any(role => role.Name == name))
        {
            var existingRole = db.AppRoles.Single(role => role.Name == name);
            var hasPermissionChanges = false;
            if (managerApprovePurchaseOrders && !existingRole.CanManagerApprovePurchaseOrders)
            {
                existingRole.CanManagerApprovePurchaseOrders = true;
                hasPermissionChanges = true;
            }

            if (approvePurchaseOrders && !existingRole.CanApprovePurchaseOrders)
            {
                existingRole.CanApprovePurchaseOrders = true;
                hasPermissionChanges = true;
            }

            if (hasPermissionChanges)
            {
                db.SaveChanges();
            }

            return;
        }

        db.AppRoles.Add(new AppRole
        {
            Name = name,
            CanAccessPurchaseOrders = purchaseOrders,
            CanManagerApprovePurchaseOrders = managerApprovePurchaseOrders,
            CanApprovePurchaseOrders = approvePurchaseOrders,
            CanAccessJobCards = jobCards,
            CanAccessWialonUnits = wialonUnits,
            CanAccessTrackingCertificates = trackingCertificates,
            CanAccessInventory = inventory,
            CanAccessConnectivitySettings = connectivitySettings,
            CanManageUsers = manageUsers
        });
        db.SaveChanges();
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
    }

    private static AppRole CopyRolePermissions(AppRole role)
    {
        return new AppRole
        {
            CanAccessPurchaseOrders = role.CanAccessPurchaseOrders,
            CanManagerApprovePurchaseOrders = role.CanManagerApprovePurchaseOrders,
            CanApprovePurchaseOrders = role.CanApprovePurchaseOrders,
            CanAccessJobCards = role.CanAccessJobCards,
            CanAccessWialonUnits = role.CanAccessWialonUnits,
            CanAccessTrackingCertificates = role.CanAccessTrackingCertificates,
            CanAccessInventory = role.CanAccessInventory,
            CanAccessConnectivitySettings = role.CanAccessConnectivitySettings,
            CanManageUsers = role.CanManageUsers
        };
    }

    private static bool IsLastActiveAdministrator(PurchaseOrderContext db, int appUserId)
    {
        var activeAdminCount = db.AppUsers
            .Include(user => user.Role)
            .Count(user => user.IsActive && user.Role.Name == AdministratorRoleName);

        var selectedUserIsAdmin = db.AppUsers
            .Include(user => user.Role)
            .Any(user => user.AppUserId == appUserId && user.IsActive && user.Role.Name == AdministratorRoleName);

        return selectedUserIsAdmin && activeAdminCount <= 1;
    }

    private static void AddColumnIfMissing(PurchaseOrderContext db, string tableName, string columnName, string columnDefinition)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }

        db.Database.ExecuteSqlRaw($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
    }

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(PasswordSaltByteCount);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            PasswordHashByteCount);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    private static bool VerifyPassword(string password, string salt, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(salt) ||
            string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(salt);
        var expectedHashBytes = Convert.FromBase64String(expectedHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            expectedHashBytes.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHashBytes);
    }
}
