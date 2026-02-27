# TASKS.md — 各子任務詳細規格

> 執行每個 Task 前，確認前一個 Task 的驗收指令已通過。
> 遇到問題先執行 `dotnet build ApiGatewayLab.sln` 確認無編譯錯誤再繼續。

---

## Task 01：建立 Solution 與專案結構

```powershell
dotnet new sln -n ApiGatewayLab

# TokenService 四層
dotnet new classlib -n TokenService.Domain         -o src/TokenService/TokenService.Domain
dotnet new classlib -n TokenService.Application    -o src/TokenService/TokenService.Application
dotnet new classlib -n TokenService.Infrastructure -o src/TokenService/TokenService.Infrastructure
dotnet new webapi   -n TokenService.API            -o src/TokenService/TokenService.API

# 其他服務
dotnet new webapi -n ApiGateway -o src/ApiGateway
dotnet new webapi -n SampleApi  -o src/SampleApi

# 測試
dotnet new xunit -n TokenService.Application.Tests -o tests/TokenService.Application.Tests

# 加入 Solution
dotnet sln add (Get-ChildItem -Recurse *.csproj | Select-Object -ExpandProperty FullName)

# 層間 ProjectReference（單向依賴）
dotnet add src/TokenService/TokenService.Application    reference src/TokenService/TokenService.Domain
dotnet add src/TokenService/TokenService.Infrastructure reference src/TokenService/TokenService.Domain
dotnet add src/TokenService/TokenService.Infrastructure reference src/TokenService/TokenService.Application
dotnet add src/TokenService/TokenService.API            reference src/TokenService/TokenService.Application
dotnet add src/TokenService/TokenService.API            reference src/TokenService/TokenService.Infrastructure
dotnet add tests/TokenService.Application.Tests         reference src/TokenService/TokenService.Domain
dotnet add tests/TokenService.Application.Tests         reference src/TokenService/TokenService.Application

# NuGet — 只有 Infrastructure 以下安裝套件
cd src/TokenService/TokenService.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package BCrypt.Net-Next
dotnet add package Microsoft.IdentityModel.JsonWebTokens

cd ../TokenService.API
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Swashbuckle.AspNetCore

cd ../../ApiGateway
dotnet add package Ocelot
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.IdentityModel.JsonWebTokens

cd ../../tests/TokenService.Application.Tests
dotnet add package Moq
dotnet add package FluentAssertions

cd ../..
dotnet build ApiGatewayLab.sln
```

**驗收：** `dotnet build` 成功；確認 Domain 和 Application `.csproj` 無任何 `<PackageReference>`

---

## Task 02：RSA 金鑰腳本 + launchSettings.json

### `scripts/generate-rsa-keys.ps1`

```powershell
param([string]$KeyDir = "src/TokenService/TokenService.API/Keys")

New-Item -ItemType Directory -Force $KeyDir               | Out-Null
New-Item -ItemType Directory -Force "src/ApiGateway/Keys" | Out-Null

$rsa = [System.Security.Cryptography.RSA]::Create(2048)
Set-Content "$KeyDir/private.pem" $rsa.ExportPkcs8PrivateKeyPem()      -NoNewline
Set-Content "$KeyDir/public.pem"  $rsa.ExportSubjectPublicKeyInfoPem() -NoNewline
Copy-Item   "$KeyDir/public.pem"  "src/ApiGateway/Keys/public.pem"
Write-Host "✅ RSA keys generated"
```

### `.gitignore`

```
**/Keys/private.pem
**/Keys/public.pem
*.db
*.db-shm
*.db-wal
```

### ⚠️ 修正：各服務 `launchSettings.json`（固定 Port，ocelot.json 路由才能對應）

**`src/TokenService/TokenService.API/Properties/launchSettings.json`**
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5001",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    }
  }
}
```

**`src/ApiGateway/Properties/launchSettings.json`**
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5000",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    }
  }
}
```

**`src/SampleApi/Properties/launchSettings.json`**
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5002",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    }
  }
}
```

### `appsettings.Development.json`（TokenService.API）

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=app.db"
  },
  "Jwt": {
    "Issuer": "https://token-service.local",
    "Audience": "api-gateway.local",
    "PrivateKeyPath": "Keys/private.pem",
    "PublicKeyPath": "Keys/public.pem",
    "AccessTokenMinutes": 60
  }
}
```

### `appsettings.Development.json`（ApiGateway）

```json
{
  "Jwt": {
    "Issuer": "https://token-service.local",
    "Audience": "api-gateway.local",
    "PublicKeyPath": "Keys/public.pem"
  }
}
```

**驗收：** `pwsh scripts/generate-rsa-keys.ps1` → `private.pem` 開頭為 `-----BEGIN PRIVATE KEY-----`

---

## Task 03：Domain 層（零 NuGet）

### `AppUser.cs`

```csharp
// src/TokenService/TokenService.Domain/Entities/AppUser.cs
// 禁止任何 annotation 或非 System 命名空間
namespace TokenService.Domain.Entities;

public class AppUser
{
    public int    Id            { get; private set; }
    public string Username      { get; private set; } = "";
    public string? PasswordHash { get; private set; }
    public string AuthType      { get; private set; } = "windows";
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public bool   IsActive      { get; private set; }

    protected AppUser() { }   // EF Core 需要無參數建構子

    public static AppUser CreateWindowsUser(string username, IEnumerable<string> roles)
        => new()
        {
            Username = username.ToLowerInvariant(),
            AuthType = "windows",
            Roles    = roles.ToList().AsReadOnly(),
            IsActive = true
        };
}
```

### `RefreshToken.cs`

```csharp
// src/TokenService/TokenService.Domain/Entities/RefreshToken.cs
// 禁止 RowVersion 欄位（EF 樂觀併發由 Infrastructure Shadow Property 處理）
namespace TokenService.Domain.Entities;

public class RefreshToken
{
    public int       Id        { get; private set; }
    public int       UserId    { get; private set; }
    public string    TokenHash { get; private set; } = "";   // SHA-256 hex
    public DateTime  ExpiresAt { get; private set; }
    public DateTime  CreatedAt { get; private set; }
    public bool      IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    protected RefreshToken() { }

    public static RefreshToken Create(int userId, string tokenHash, DateTime expiresAt)
        => new()
        {
            UserId    = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }

    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
}
```

### Repository 介面

```csharp
// src/TokenService/TokenService.Domain/Interfaces/IUserRepository.cs
namespace TokenService.Domain.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(int id, CancellationToken ct = default);
}

// src/TokenService/TokenService.Domain/Interfaces/IRefreshTokenRepository.cs
namespace TokenService.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllForUserAsync(int userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**驗收：** `TokenService.Domain.csproj` 無 `<PackageReference>`

---

## Task 04：Application 層（零 NuGet）

### Port Interfaces

```csharp
// src/TokenService/TokenService.Application/Interfaces/IAccessTokenService.cs
namespace TokenService.Application.Interfaces;
public interface IAccessTokenService { string Generate(AppUser user); }

// src/TokenService/TokenService.Application/Interfaces/IRefreshTokenGenerator.cs
namespace TokenService.Application.Interfaces;
public interface IRefreshTokenGenerator
{
    (string rawToken, string tokenHash) Generate();
}
```

### DTOs

```csharp
// src/TokenService/TokenService.Application/DTOs/TokenRequest.cs
using System.Text.Json.Serialization;
namespace TokenService.Application.DTOs;

public record TokenRequest(
    [property: JsonPropertyName("grant_type")] string GrantType,
    [property: JsonPropertyName("client_id")]  string? ClientId);

public record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken);

public record TokenResponse(
    [property: JsonPropertyName("access_token")]  string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("token_type")]    string TokenType,
    [property: JsonPropertyName("expires_in")]    int    ExpiresIn,
    [property: JsonPropertyName("scope")]         string Scope);
```

### ⚠️ 修正：自訂例外（不引 Wilson NuGet）

```csharp
// src/TokenService/TokenService.Application/Exceptions/ConcurrencyException.cs
namespace TokenService.Application.Exceptions;
public class ConcurrencyException(string message, Exception? inner = null)
    : Exception(message, inner);

// src/TokenService/TokenService.Application/Exceptions/TokenReuseException.cs
// ✅ 取代 SecurityTokenException（SecurityTokenException 屬 Microsoft.IdentityModel.Tokens，
//    引入該套件會違反 Application 層零 NuGet 原則）
namespace TokenService.Application.Exceptions;
public class TokenReuseException(string message)
    : Exception(message);
```

### RefreshTokenHasher

```csharp
// src/TokenService/TokenService.Application/UseCases/RefreshTokenHasher.cs
using System.Security.Cryptography;
using System.Text;
namespace TokenService.Application.UseCases;

public static class RefreshTokenHasher
{
    public static string ComputeHex(string rawToken)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));
    }
}
```

**驗收：** `TokenService.Application.csproj` 無 `<PackageReference>`

---

## Task 05：Application 層 — Use Cases

### IssueTokenUseCase

```csharp
// src/TokenService/TokenService.Application/UseCases/IssueTokenUseCase.cs
// 禁止 using EntityFrameworkCore 或 Microsoft.IdentityModel.*
using TokenService.Application.DTOs;
using TokenService.Application.Interfaces;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Application.UseCases;

public class IssueTokenUseCase(
    IUserRepository         users,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenService     tokenService,
    IRefreshTokenGenerator  rtGenerator)
{
    public async Task<TokenResponse> ExecuteAsync(string windowsUsername, CancellationToken ct = default)
    {
        var username = NormalizeWindowsUsername(windowsUsername);

        var user = await users.FindByUsernameAsync(username, ct)
            ?? throw new UnauthorizedAccessException($"User '{username}' not found.");

        var at              = tokenService.Generate(user);
        var (rawRt, rtHash) = rtGenerator.Generate();
        var rt              = RefreshToken.Create(user.Id, rtHash, DateTime.UtcNow.AddDays(30));

        await refreshTokens.AddAsync(rt, ct);
        await refreshTokens.SaveChangesAsync(ct);

        var scope = string.Join(" ", user.Roles.Where(r => r.Contains('.')));
        return new TokenResponse(at, rawRt, "Bearer", 3600, scope);
    }

    private static string NormalizeWindowsUsername(string raw)
        => (raw.Contains('\\') ? raw.Split('\\')[1] : raw).ToLowerInvariant();
}
```

### RefreshTokenUseCase

```csharp
// src/TokenService/TokenService.Application/UseCases/RefreshTokenUseCase.cs
// ✅ 使用 TokenReuseException（自訂，零 NuGet），不使用 SecurityTokenException
using TokenService.Application.DTOs;
using TokenService.Application.Exceptions;
using TokenService.Application.Interfaces;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Application.UseCases;

public class RefreshTokenUseCase(
    IRefreshTokenRepository refreshTokens,
    IUserRepository         users,
    IAccessTokenService     tokenService,
    IRefreshTokenGenerator  rtGenerator)
{
    private const int GracePeriodSeconds = 60;

    public async Task<TokenResponse> ExecuteAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenHasher.ComputeHex(rawRefreshToken);
        var rt   = await refreshTokens.FindByHashAsync(hash, ct)
            ?? throw new UnauthorizedAccessException("invalid_token");

        if (rt.IsRevoked)
        {
            var graceCutoff = rt.RevokedAt?.AddSeconds(GracePeriodSeconds) ?? DateTime.MinValue;
            if (DateTime.UtcNow > graceCutoff)
            {
                await refreshTokens.RevokeAllForUserAsync(rt.UserId, ct);
                await refreshTokens.SaveChangesAsync(ct);
                throw new TokenReuseException("token_reuse_detected");  // ✅ 自訂，非 Wilson
            }
        }

        if (rt.IsExpired)
            throw new UnauthorizedAccessException("token_expired");

        rt.Revoke();
        await refreshTokens.SaveChangesAsync(ct);   // 樂觀併發由 Repository 捕捉後重拋

        // ✅ FindByIdAsync(int)，不得用 FindByUsernameAsync(userId.ToString())
        var user = await users.FindByIdAsync(rt.UserId, ct)
            ?? throw new UnauthorizedAccessException("user_not_found");

        var newAt           = tokenService.Generate(user);
        var (rawRt2, hash2) = rtGenerator.Generate();
        await refreshTokens.AddAsync(RefreshToken.Create(rt.UserId, hash2, DateTime.UtcNow.AddDays(30)), ct);
        await refreshTokens.SaveChangesAsync(ct);

        return new TokenResponse(newAt, rawRt2, "Bearer", 3600, "");
    }
}
```

---

## Task 06：Infrastructure — EF + DbContext + DataSeeder

### ⚠️ 修正：SQLite 不支援 IsRowVersion()，改用 IsConcurrencyToken()

```csharp
// src/TokenService/TokenService.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs
// IsRowVersion() 是 SQL Server 專有功能，SQLite 使用 IsConcurrencyToken() 替代
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TokenHash).HasMaxLength(100).IsRequired();

        // ✅ SQLite 相容的樂觀併發：Shadow Property + IsConcurrencyToken()
        //    每次 SaveChanges 前手動更新 RowVersion 的值（見 AppDbContext.SaveChangesAsync override）
        builder.Property<Guid>("RowVersion")
               .IsConcurrencyToken()
               .HasDefaultValueSql("(lower(hex(randomblob(16))))");   // SQLite UUID

        builder.HasOne<AppUser>()
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### AppUserConfiguration

```csharp
// src/TokenService/TokenService.Infrastructure/Persistence/Configurations/AppUserConfiguration.cs
public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.AuthType).HasMaxLength(20).IsRequired();

        // IReadOnlyList<string> Roles → JSON 字串儲存
        builder.Property(u => u.Roles)
               .HasConversion(
                   v => System.Text.Json.JsonSerializer.Serialize(v,
                            (System.Text.Json.JsonSerializerOptions?)null),
                   v => (IReadOnlyList<string>)System.Text.Json.JsonSerializer
                            .Deserialize<List<string>>(v,
                            (System.Text.Json.JsonSerializerOptions?)null)!)
               .HasColumnName("RolesJson")
               .HasMaxLength(500)
               .IsRequired();
    }
}
```

### AppDbContext（覆寫 SaveChangesAsync 以更新 RowVersion）

```csharp
// src/TokenService/TokenService.Infrastructure/Persistence/AppDbContext.cs
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser>      Users         => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfiguration(new AppUserConfiguration());
        mb.ApplyConfiguration(new RefreshTokenConfiguration());
        // 禁止 HasData()，Seed 由 DataSeeder.SeedAsync() 在啟動時執行
    }

    // ✅ 每次 SaveChanges 前自動更新 RowVersion（IsConcurrencyToken 需手動更新 value）
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<RefreshToken>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid();
        }
        return base.SaveChangesAsync(ct);
    }
}
```

### DataSeeder

```csharp
// src/TokenService/TokenService.Infrastructure/Persistence/DataSeeder.cs
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;   // 冪等設計

        db.Users.AddRange(
            AppUser.CreateWindowsUser("jeff.wang",  ["admin", "orders.read", "orders.write"]),
            AppUser.CreateWindowsUser("alice.chen", ["viewer", "orders.read"])
        );
        await db.SaveChangesAsync();
    }
}
```

### ⚠️ 修正：EF Migration 指令需明確指定 --project 和 --startup-project

```powershell
# 在 Solution 根目錄執行
cd src/TokenService/TokenService.API

dotnet ef migrations add InitialCreate `
  --project     ../TokenService.Infrastructure/TokenService.Infrastructure.csproj `
  --startup-project TokenService.API.csproj `
  --output-dir  ../TokenService.Infrastructure/Persistence/Migrations

dotnet ef database update `
  --project     ../TokenService.Infrastructure/TokenService.Infrastructure.csproj `
  --startup-project TokenService.API.csproj
```

**驗收：** `app.db` 存在，`sqlite3 app.db ".tables"` 顯示 Users / RefreshTokens 兩張表

---

## Task 07：Infrastructure — Repositories + Security

### UserRepository

```csharp
// src/TokenService/TokenService.Infrastructure/Persistence/Repositories/UserRepository.cs
public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByUsernameAsync(string username, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

    public Task<AppUser?> FindByIdAsync(int id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
}
```

### RefreshTokenRepository

```csharp
// src/TokenService/TokenService.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs
// 禁止 .Include()：Domain RefreshToken 無 AppUser navigation property
public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(token, ct);

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync(ct);
        tokens.ForEach(t => t.Revoke());
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try   { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex)
              { throw new ConcurrencyException("RT 已被並發修改，請重試。", ex); }
    }
}
```

### ⚠️ 修正：RSA 物件必須 Dispose（避免記憶體洩漏）

```csharp
// src/TokenService/TokenService.Infrastructure/Security/JwtSettings.cs
namespace TokenService.Infrastructure.Security;
public record JwtSettings(
    string Issuer,
    string Audience,
    string PrivateKeyPath,
    string PublicKeyPath,
    int    AccessTokenMinutes = 60);

// src/TokenService/TokenService.Infrastructure/Security/RsaAccessTokenService.cs
// ✅ using var rsa — 確保 RSA 物件在方法結束後釋放
public class RsaAccessTokenService(JwtSettings settings) : IAccessTokenService
{
    public string Generate(AppUser user)
    {
        using var rsa = RSA.Create();   // ✅ using var，修正原本的 RSA.Create() 洩漏
        rsa.ImportFromPem(File.ReadAllText(settings.PrivateKeyPath));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub,  user.Username),
                new Claim(JwtRegisteredClaimNames.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
                new Claim("roles",
                          JsonSerializer.Serialize(user.Roles),
                          JsonClaimValueTypes.JsonArray),
                new Claim("scope",
                          string.Join(" ", user.Roles.Where(r => r.Contains('.'))))
            ]),
            Issuer    = settings.Issuer,
            Audience  = settings.Audience,
            IssuedAt  = DateTime.UtcNow,
            Expires   = DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        });
    }
}

// src/TokenService/TokenService.Infrastructure/Security/CryptoRefreshTokenGenerator.cs
public class CryptoRefreshTokenGenerator : IRefreshTokenGenerator
{
    public (string rawToken, string tokenHash) Generate()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, RefreshTokenHasher.ComputeHex(raw));
    }
}
```

> **⚠️ 進階優化**（Lab 階段可選）：`RsaAccessTokenService` 目前每次呼叫都讀取 PEM 檔。
> 如果 Token 簽發頻繁，可將 `RsaSecurityKey` 在建構子快取為 `readonly` 欄位。
> Lab 階段因 SQLite 已是瓶頸，此優化可延後處理。

---

## Task 08：API 層

### DevWindowsAuthMiddleware

```csharp
// src/TokenService/TokenService.API/Middleware/DevWindowsAuthMiddleware.cs
namespace TokenService.API.Middleware;

public class DevWindowsAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var devUser = ctx.Request.Headers["X-Dev-Windows-User"].FirstOrDefault();
        if (!string.IsNullOrEmpty(devUser))
            ctx.Items["WindowsUser"] = devUser;   // e.g. "CORP\jeff.wang"
        await next(ctx);
    }
}
```

### TokenController

```csharp
// src/TokenService/TokenService.API/Controllers/TokenController.cs
// ✅ Controller 只注入 UseCase（禁止注入 DbContext / Repository）
// ✅ catch TokenReuseException（自訂）而非 SecurityTokenException（Wilson）
using TokenService.Application.DTOs;
using TokenService.Application.Exceptions;
using TokenService.Application.UseCases;

[ApiController, Route("api/token")]
public class TokenController(
    IssueTokenUseCase   issueToken,
    RefreshTokenUseCase refreshToken) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Token([FromBody] TokenRequest req, CancellationToken ct)
    {
        if (req.GrantType != "windows_identity")
            return BadRequest(new { error = "unsupported_grant_type" });

        var windowsUser = HttpContext.Items["WindowsUser"] as string
                       ?? HttpContext.User?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(windowsUser))
            return Unauthorized(new { error = "windows_auth_required" });

        try   { return Ok(await issueToken.ExecuteAsync(windowsUser, ct)); }
        catch (UnauthorizedAccessException ex)
              { return Unauthorized(new { error = "unauthorized",
                                          error_description = ex.Message }); }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        try   { return Ok(await refreshToken.ExecuteAsync(req.RefreshToken, ct)); }
        catch (TokenReuseException)               // ✅ 自訂，不需 Wilson NuGet
              { return Unauthorized(new { error = "token_reuse_detected" }); }
        catch (UnauthorizedAccessException ex)
              { return Unauthorized(new { error = ex.Message }); }
        catch (ConcurrencyException)
              { return StatusCode(503, new { error = "concurrent_request" }); }
    }
}
```

### Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using TokenService.Application.Interfaces;
using TokenService.Application.UseCases;
using TokenService.Domain.Interfaces;
using TokenService.Infrastructure.Persistence;
using TokenService.Infrastructure.Persistence.Repositories;
using TokenService.Infrastructure.Security;
using TokenService.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt section missing.");
builder.Services.AddSingleton(jwtSettings);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));

builder.Services.AddScoped<IUserRepository,         UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAccessTokenService,     RsaAccessTokenService>();
builder.Services.AddScoped<IRefreshTokenGenerator,  CryptoRefreshTokenGenerator>();
builder.Services.AddScoped<IssueTokenUseCase>();
builder.Services.AddScoped<RefreshTokenUseCase>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DataSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseMiddleware<DevWindowsAuthMiddleware>();
}

app.MapControllers();
app.Run();
```

**驗收：**
```powershell
cd src/TokenService/TokenService.API && dotnet run
# Swagger：http://localhost:5001/swagger
# POST /api/token（帶 X-Dev-Windows-User: CORP\jeff.wang）→ 200 + AT + RT
```

---

## Task 09：ApiGateway — Ocelot + Rate Limiter

### `ocelot.json`

```json
{
  "Routes": [
    {
      "UpstreamPathTemplate": "/api/token/{everything}",
      "UpstreamHttpMethod": ["POST"],
      "DownstreamPathTemplate": "/api/token/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5001 }],
      "RateLimitOptions": { "EnableRateLimiting": false }
    },
    {
      "UpstreamPathTemplate": "/api/orders/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
      "DownstreamPathTemplate": "/api/orders/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5002 }],
      "AuthenticationOptions": { "AuthenticationProviderKey": "Bearer" },
      "AddHeadersToRequest": {
        "X-User-Id":    "Claims[sub] > value",
        "X-User-Roles": "Claims[roles] > value"
      },
      "RateLimitOptions": {
        "EnableRateLimiting": true,
        "Period": "1s",
        "Limit": 10,
        "PeriodTimespan": 1
      }
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5000",
    "RateLimitOptions": { "HttpStatusCode": 429 }
  }
}
```

### `Program.cs`

```csharp
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "Keys", "public.pem");

// ✅ using var 釋放 RSA 資源（只用於建構 RsaSecurityKey）
RsaSecurityKey rsaKey;
using (var rsa = RSA.Create())
{
    rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
    rsaKey = new RsaSecurityKey(rsa.ExportParameters(false)); // 複製參數後可安全 Dispose
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = rsaKey,
            ClockSkew                = TimeSpan.FromSeconds(10)
        };
    });

builder.Services.AddRateLimiter(opt =>
{
    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var key = ctx.User?.FindFirst("sub")?.Value
               ?? ctx.Connection.RemoteIpAddress?.ToString()
               ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromSeconds(1) });
    });
    opt.RejectionStatusCode = 429;
});

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();
app.UseAuthentication();
// ⚠️ UseRateLimiter 必須在 UseOcelot 之前
app.UseRateLimiter();
await app.UseOcelot();
app.Run();
```

---

## Task 10：SampleApi

```csharp
// src/SampleApi/Controllers/OrdersController.cs
[ApiController, Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpGet]
    public IActionResult GetOrders()
    {
        var userId    = Request.Headers["X-User-Id"].ToString();
        var userRoles = Request.Headers["X-User-Roles"].ToString();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Missing X-User-Id" });

        return Ok(new
        {
            requestedBy = userId,
            roles       = userRoles,
            orders = new[]
            {
                new { id = 1, product = "Widget A", qty = 10 },
                new { id = 2, product = "Widget B", qty = 5  }
            }
        });
    }
}
```

---

## Task 11：單元測試（Application 層）

```csharp
// tests/TokenService.Application.Tests/IssueTokenUseCaseTests.cs
public class IssueTokenUseCaseTests
{
    private readonly Mock<IUserRepository>         _users         = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IAccessTokenService>     _tokenService  = new();
    private readonly Mock<IRefreshTokenGenerator>  _rtGenerator   = new();

    private IssueTokenUseCase Sut() => new(
        _users.Object, _refreshTokens.Object, _tokenService.Object, _rtGenerator.Object);

    [Fact]
    public async Task Execute_ValidUser_ReturnsToken()
    {
        var user = AppUser.CreateWindowsUser("jeff.wang", ["orders.read"]);
        _users.Setup(r => r.FindByUsernameAsync("jeff.wang", default)).ReturnsAsync(user);
        _tokenService.Setup(s => s.Generate(user)).Returns("fake-at");
        _rtGenerator.Setup(g => g.Generate()).Returns(("raw-rt", "hash"));

        var result = await Sut().ExecuteAsync(@"CORP\jeff.wang");

        result.AccessToken.Should().Be("fake-at");
        result.RefreshToken.Should().Be("raw-rt");
    }

    [Fact]
    public async Task Execute_UserNotFound_Throws()
    {
        _users.Setup(r => r.FindByUsernameAsync(It.IsAny<string>(), default))
              .ReturnsAsync((AppUser?)null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Sut().ExecuteAsync("ghost"));
    }

    [Theory]
    [InlineData(@"CORP\jeff.wang", "jeff.wang")]
    [InlineData("JEFF.WANG",       "jeff.wang")]
    public async Task Execute_NormalizesUsername(string input, string expected)
    {
        var user = AppUser.CreateWindowsUser(expected, []);
        _users.Setup(r => r.FindByUsernameAsync(expected, default)).ReturnsAsync(user);
        _tokenService.Setup(s => s.Generate(It.IsAny<AppUser>())).Returns("at");
        _rtGenerator.Setup(g => g.Generate()).Returns(("rt", "h"));

        await Sut().ExecuteAsync(input);

        _users.Verify(r => r.FindByUsernameAsync(expected, default), Times.Once);
    }
}
```

---

## Task 12：端對端測試

### `tests/IntegrationTests/Flow1_WindowsAuth.http`

```http
### Step 1: 取得 AT + RT
POST http://localhost:5000/api/token
Content-Type: application/json
X-Dev-Windows-User: CORP\jeff.wang

{ "grant_type": "windows_identity" }

### Step 2: 呼叫業務 API
GET http://localhost:5000/api/orders
Authorization: Bearer {{access_token}}

### Step 3: 換發 Token
POST http://localhost:5000/api/token/refresh
Content-Type: application/json
{ "refresh_token": "{{refresh_token}}" }

### Step 4: 重用舊 RT（預期 401）
POST http://localhost:5000/api/token/refresh
Content-Type: application/json
{ "refresh_token": "{{old_refresh_token}}" }
```

### `scripts/test-flow.ps1`

```powershell
$base = "http://localhost:5000"; $pass = 0; $fail = 0
function Assert($ok, $name) {
    if ($ok) { Write-Host "  ✅ $name" -ForegroundColor Green;  $global:pass++ }
    else      { Write-Host "  ❌ $name" -ForegroundColor Red;    $global:fail++ }
}

Write-Host "`n=== API Gateway Lab — E2E Test ===`n"

$r1 = Invoke-RestMethod "$base/api/token" -Method POST `
    -Headers @{"X-Dev-Windows-User"="CORP\jeff.wang"} `
    -Body '{"grant_type":"windows_identity"}' -ContentType "application/json" -EA SilentlyContinue
Assert ($r1.access_token)  "取得 AT"
Assert ($r1.refresh_token) "取得 RT"
$at = $r1.access_token; $rt = $r1.refresh_token

$parts = $at.Split('.'); $padded = $parts[1].PadRight(($parts[1].Length+3) -band -bnot 3,'=')
$p = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($padded)) | ConvertFrom-Json
Assert ($p.sub -eq "jeff.wang")                    "AT.sub 正確"
Assert ($p.iss -eq "https://token-service.local")  "AT.iss 正確"
Assert ($p.jti)                                    "AT.jti 存在"

$r3 = Invoke-WebRequest "$base/api/orders" -Headers @{Authorization="Bearer $at"} -EA SilentlyContinue
Assert ($r3.StatusCode -eq 200) "GET /api/orders 200"
Assert (($r3.Content | ConvertFrom-Json).requestedBy -eq "jeff.wang") "X-User-Id 正確"

$r5 = Invoke-RestMethod "$base/api/token/refresh" -Method POST `
    -Body "{`"refresh_token`":`"$rt`"}" -ContentType "application/json" -EA SilentlyContinue
Assert ($r5.access_token)              "RT 換發成功"
Assert ($r5.refresh_token -ne $rt)    "RT Rotation（新舊不同）"

Start-Sleep 65
try   { Invoke-RestMethod "$base/api/token/refresh" -Method POST `
          -Body "{`"refresh_token`":`"$rt`"}" -ContentType "application/json"
        Assert $false "重用舊 RT 應 401" }
catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 401) "重用舊 RT → 401" }

$r7 = Invoke-WebRequest "$base/api/orders" -Headers @{Authorization="Bearer bad.token.here"} -EA SilentlyContinue
Assert ($r7.StatusCode -eq 401) "無效 AT → 401"

$rl = $false
for ($i=0; $i -lt 25; $i++) {
    $rx = Invoke-WebRequest "$base/api/orders" -Headers @{Authorization="Bearer $at"} -EA SilentlyContinue
    if ($rx.StatusCode -eq 429) { $rl = $true; break }
}
Assert $rl "Rate Limit → 429"

Write-Host "`n=== PASS=$pass  FAIL=$fail ===`n"
if ($fail -gt 0) { exit 1 }
```

---

## Task 13：README.md

產生 `README.md`，包含：
1. 專案說明（一段話）
2. 前置需求（.NET 8 SDK、PowerShell 7+）
3. 快速啟動（generate-rsa-keys → ef migrate → dotnet run × 3）
4. 執行測試
5. 已知限制（Dev Mock、SQLite、HTTP only）
6. 生產環境對應事項（Oracle、Azure Key Vault、IIS Negotiate）
