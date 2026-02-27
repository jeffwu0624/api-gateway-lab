# ARCHITECTURE.md — 架構說明

## 元件拓樸

```
┌────────────────────────────────────────────────────────────────┐
│                    本機 Lab 元件拓樸（無 Docker）                  │
│                                                                │
│  Browser / Postman / REST Client                               │
│        │                                                       │
│        ▼ :5000（唯一對外入口）                                    │
│  ┌─────────────┐    /api/token/**（白名單，不驗 AT）              │
│  │ API Gateway │──────────────────────────────────────┐       │
│  │  (Ocelot)   │                                      ▼ :5001 │
│  │             │                          ┌───────────────────┐│
│  └─────────────┘   /api/orders/**          │  Token Service    ││
│        │           驗 AT（本地 RSA 公鑰）   │  (ASP.NET Core 8) ││
│        │           注入 X-User-Id/Roles    │   Clean Arch 4層   ││
│        ▼ :5002     ────────────────────▶  └────────┬──────────┘│
│  ┌─────────────┐                                   │           │
│  │ Sample API  │                         ┌─────────▼─────────┐ │
│  │  (Orders)   │                         │    SQLite DB      │ │
│  │   :5002     │                         │  (app.db, EF 8)   │ │
│  └─────────────┘                         └───────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

---

## TokenService Clean Architecture 分層

```
TokenService.Domain          （零 NuGet）
  └── Entities/
  │     AppUser             純 POCO，工廠方法 CreateWindowsUser()
  │     RefreshToken        純 POCO，Revoke() + IsExpired 計算屬性
  └── Interfaces/
        IUserRepository     FindByUsernameAsync / FindByIdAsync(int)
        IRefreshTokenRepository

         ↑ 依賴
TokenService.Application     （零 NuGet）
  └── Interfaces/
  │     IAccessTokenService  Generate(AppUser) → string AT
  │     IRefreshTokenGenerator Generate() → (rawToken, tokenHash)
  ├── UseCases/
  │     IssueTokenUseCase    windows_identity → AT + RT
  │     RefreshTokenUseCase  rawRT → 新 AT + 新 RT（Rotation + 樂觀併發）
  │     RefreshTokenHasher   SHA-256 hash 唯一入口
  ├── DTOs/
  │     TokenRequest / RefreshRequest / TokenResponse
  └── Exceptions/
        ConcurrencyException

         ↑ 依賴
TokenService.Infrastructure  （EF / JWT / BCrypt）
  ├── Persistence/
  │     AppDbContext          ApplyConfiguration 方式，無 HasData()
  │     Configurations/
  │       AppUserConfiguration       Fluent API
  │       RefreshTokenConfiguration  Shadow Property "RowVersion"
  │     Repositories/
  │       UserRepository             FindByIdAsync(int id)
  │       RefreshTokenRepository     無 .Include()，SaveChanges 捕捉 Concurrency
  │     DataSeeder                   冪等 Seed，取代外部腳本
  └── Security/
        JwtSettings                  record，強型別設定
        RsaAccessTokenService        注入 JwtSettings，不注入 IConfiguration
        CryptoRefreshTokenGenerator  RandomNumberGenerator.GetBytes(64)

         ↑ 依賴
TokenService.API             （ASP.NET Core 8）
  ├── Controllers/
  │     TokenController      薄 Controller，只注入 Use Case
  ├── Middleware/
  │     DevWindowsAuthMiddleware  Development only
  └── Program.cs             DI 組裝唯一入口
```

---

## Port 規劃

| 服務 | 本機 Port | 說明 |
|------|---------|------|
| API Gateway | 5000 | 唯一對外入口，白名單 `/api/token/**` 不驗 AT |
| Token Service | 5001 | 白名單路由轉發，非公開 |
| Sample API | 5002 | 內網服務，信任 Gateway Header |

---

## Flow ①：網內員工瀏覽器呼叫 API（Dev 模式序列）

```
瀏覽器           API Gateway     Token Service      SQLite DB    Sample API
   │                 │                 │                 │             │
   │ ①POST /api/token│                 │                 │             │
   │ X-Dev-Windows-User: CORP\jeff.wang│                 │             │
   │────────────────▶│                 │                 │             │
   │                 │ ②白名單轉發     │                 │             │
   │                 │────────────────▶│                 │             │
   │                 │                 │③DevMiddleware   │             │
   │                 │                 │  讀Header注入   │             │
   │                 │                 │ ④IssueTokenUseCase           │
   │                 │                 │   FindByUsernameAsync         │
   │                 │                 │────────────────▶│             │
   │                 │                 │ ⑤回傳 AppUser   │             │
   │                 │                 │◀────────────────│             │
   │                 │                 │ ⑥Generate AT    │             │
   │                 │                 │  Generate RT    │             │
   │                 │                 │  Save RT Hash   │             │
   │                 │                 │────────────────▶│             │
   │ ⑦{AT, RT, ...} │                 │                 │             │
   │◀────────────────│◀────────────────│                 │             │
   │                 │                 │                 │             │
   │ ⑧GET /api/orders│                 │                 │             │
   │ Authorization: Bearer AT          │                 │             │
   │────────────────▶│                 │                 │             │
   │                 │⑨驗 AT（RSA 公鑰）│                │             │
   │                 │  本地驗證，不回 Token Service      │             │
   │                 │⑩注入 X-User-Id  │                 │             │
   │                 │  X-User-Roles   │                 │             │
   │                 │─────────────────────────────────────────────────▶│
   │                 │                 │                 │ ⑪業務邏輯   │
   │ ⑫回傳訂單資料   │                 │                 │◀────────────│
   │◀────────────────│◀────────────────────────────────────────────────│
```

---

## RT Rotation 狀態機

```
RT 狀態
  │
  ├─ Hash 不存在 ────────────────────────────── 401 invalid_token
  │
  ├─ IsRevoked = true
  │       ├─ RevokedAt + 60s > now ─────────── 寬限期，正常換發
  │       └─ RevokedAt + 60s ≤ now ─────────── 撤銷所有 RT + 401 token_reuse_detected
  │
  ├─ IsExpired = true ───────────────────────── 401 token_expired
  │
  └─ 正常 Active
          ├─ rt.Revoke() + SaveChanges
          │       └─ DbUpdateConcurrencyException → 503（Application ConcurrencyException）
          └─ 成功 → FindByIdAsync(rt.UserId) → Generate 新 AT + 新 RT
```

---

## Shadow Property（EF 樂觀併發）

```
Domain Entity（RefreshToken）       EF Infrastructure 層
┌──────────────────────────┐       ┌──────────────────────────────────┐
│  int       Id            │       │  builder.Property<byte[]>        │
│  int       UserId        │       │    ("RowVersion").IsRowVersion(); │
│  string    TokenHash     │  ←──  │                                  │
│  DateTime  ExpiresAt     │       │  Domain Entity 完全不知道         │
│  bool      IsRevoked     │       │  RowVersion 欄位的存在            │
│  DateTime? RevokedAt     │       │                                  │
│  (無 RowVersion)         │       │  DbUpdateConcurrencyException     │
└──────────────────────────┘       │  → ConcurrencyException（App層）  │
                                   └──────────────────────────────────┘
```

---

## 依賴方向驗證

```
Domain       ← 零依賴（無 NuGet，無 ProjectReference）
Application  ← 只依賴 Domain
Infrastructure ← 依賴 Domain + Application（實作 Port Interfaces）
API          ← 依賴 Application + Infrastructure（DI 組裝）

❌ 禁止方向：
  Domain       → Application / Infrastructure / API
  Application  → Infrastructure / API
  Infrastructure → API
```

---

## 生產環境對應

| Lab 設計 | 生產環境替換 | 備註 |
|---------|------------|------|
| `X-Dev-Windows-User` Header | IIS In-Process + Negotiate 驗證 | `Program.cs` 不需改，Middleware 自動停用 |
| SQLite `app.db` | Oracle DB + ODP.NET | 改 `UseSqlite` → `UseOracle`，EF Migration 重建 |
| `Keys/private.pem` 本機檔案 | Azure Key Vault | `JwtSettings.PrivateKeyPath` 改為 Key Vault 參考 |
| HTTP | HTTPS + TLS 1.2+ + HSTS | IIS 或 Nginx 終止 TLS |
| `dotnet run` 三個終端機 | IIS / Windows Service / Systemd | 正式部署方案 |
