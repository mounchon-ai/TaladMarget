namespace Talad.Domain.Accounts;

/// <summary>ENT-008 · บัญชีผู้ใช้ (พนักงานขาย · เจ้าของร้าน). Status follows STM-talad-004.</summary>
public class UserAccount
{
    public int Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public UserRole Role { get; private set; }

    /// <summary>Stored through .NET Identity's hasher only — never a readable password (ENT-008.passwordHash).</summary>
    public string PasswordHash { get; private set; } = null!;

    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private UserAccount() { } // EF

    public UserAccount(string username, string displayName, UserRole role, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("username must not be empty", nameof(username));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("displayName must not be empty", nameof(displayName));
        Username = username;
        DisplayName = displayName;
        Role = role;
        Status = AccountStatus.Active; // STM-talad-004 initial
        CreatedAt = createdAt;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash)) throw new ArgumentException("passwordHash must not be empty", nameof(passwordHash));
        PasswordHash = passwordHash;
    }

    public bool CanSignIn => Status == AccountStatus.Active;
}

/// <summary>ENT-008.role</summary>
public enum UserRole
{
    Cashier, // ROLE-001 พนักงานขาย
    Owner,   // ROLE-002 เจ้าของร้าน/ผู้ดูแล
}

/// <summary>STM-talad-004</summary>
public enum AccountStatus
{
    Active,
    Disabled,
}
