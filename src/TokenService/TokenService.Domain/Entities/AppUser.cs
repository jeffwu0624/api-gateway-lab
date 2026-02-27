namespace TokenService.Domain.Entities;

public class AppUser
{
    public int Id { get; private set; }
    public string Username { get; private set; } = "";
    public string? PasswordHash { get; private set; }
    public string AuthType { get; private set; } = "windows";
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public bool IsActive { get; private set; }

    protected AppUser() { }

    public static AppUser CreateWindowsUser(string username, IEnumerable<string> roles)
        => new()
        {
            Username = username.ToLowerInvariant(),
            AuthType = "windows",
            Roles = roles.ToList().AsReadOnly(),
            IsActive = true
        };
}
