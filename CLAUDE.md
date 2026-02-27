# API Gateway Lab — 網內 Web 呼叫 API 驗證流程

## 專案目的

本機重現「公司內部員工瀏覽器 → API Gateway → 後端 API」完整 JWT 驗證流程。
使用 Dev Header 模擬 Windows 身分，無需 AD/IIS 環境即可開發與測試。

詳細架構說明見 @docs/ARCHITECTURE.md
各子任務完整規格見 @docs/TASKS.md

---

## 技術棧

- **Token Service**：ASP.NET Core 8，Clean Architecture 四層（Domain / Application / Infrastructure / API）
- **API Gateway**：Ocelot 23.x + .NET 8 內建 RateLimiter
- **後端 API**：ASP.NET Core 8 SampleApi
- **資料庫**：SQLite（本機 Lab；生產換 Oracle 只需改 EF Provider）
- **簽章**：RSA 2048-bit，AT 用 RS256，公鑰存 Gateway，私鑰存 TokenService

---

## 常用指令

```bash
# 建置整個 Solution
dotnet build ApiGatewayLab.sln

# 產生 RSA 金鑰（首次執行）
pwsh scripts/generate-rsa-keys.ps1

# 套用 EF Migration（在 TokenService.API 目錄下執行）
cd src/TokenService/TokenService.API
dotnet ef database update --project ../TokenService.Infrastructure

# 啟動服務（三個終端機）
cd src/TokenService/TokenService.API && dotnet run   # :5001
cd src/ApiGateway                   && dotnet run   # :5000
cd src/SampleApi                    && dotnet run   # :5002

# 執行單元測試
dotnet test tests/TokenService.Application.Tests

# 執行端對端測試
pwsh scripts/test-flow.ps1
```

---

## 開發流程（TDD + 小步開發 + Code Review）

Claude 開發程式碼時**必須**遵守以下流程：

### TDD 三步循環（Red → Green → Refactor）

1. **Red**：先寫一個失敗的測試，明確定義預期行為
2. **Green**：寫最少量的產品程式碼讓測試通過
3. **Refactor**：在測試全綠的前提下重構，消除重複與壞味道

### 小步開發原則

- 每一步只做一件事：新增一個方法、一個類別、一條邏輯分支
- 每一步結束時必須：程式可編譯、測試全通過
- 若某步驟過大，拆分為更小的子步驟

### 每一小步完成後執行 Code Review

每完成一個小步驟（測試通過後），Claude **必須**自行執行 code review，檢查：

1. **Clean Architecture 合規**：是否違反層間依賴方向
2. **SOLID 原則**：單一職責、開放封閉、依賴反轉是否被遵守
3. **測試品質**：測試命名是否清晰、斷言是否精準、是否涵蓋邊界條件
4. **安全性**：是否有注入風險、敏感資訊洩漏、資源未釋放
5. **程式碼異味**：重複邏輯、過長方法、魔術數字、命名不當

### 自動修正循環

Review 發現問題時，**必須**立即修正，並重新執行 code review，反覆循環直到無問題為止：

```
Code Review → 發現問題 → 修正 → 再次 Code Review → … → 全部通過 → 進入下一步
```

- 不得跳過修正，不累積技術債
- 每次修正後重新跑測試，確保不引入回歸問題

### Commit 規範

當一個小步驟通過最終 code review（無剩餘問題）後，以**中文**撰寫 commit message 說明本次變更的意圖，格式：

```
<類型>: <意圖說明>

<選填：補充細節>

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
```

類型包含：`功能`、`修正`、`重構`、`測試`、`文件`、`建置`

---

## Clean Architecture 依賴規則

```
Domain (零NuGet) ← Application (零NuGet) ← Infrastructure (EF/JWT/BCrypt) ← API
```

Claude 產生程式碼時**必須**遵守：
- Domain / Application `.csproj` 不得有任何 `<PackageReference>`
- `Application` 層不得 using `Microsoft.EntityFrameworkCore` 或 `Microsoft.IdentityModel.*`
- `Controller` 只注入 UseCase，不注入 Repository 或 DbContext
- EF 設定全部在 `Infrastructure/Configurations/` 用 Fluent API，Domain Entity 無 annotation

---

## 關鍵設計決策（勿改動）

| 項目 | 決策 | 原因 |
|------|------|------|
| `SecurityTokenException` | 改用自訂 `TokenReuseException`（Application/Exceptions） | Application 層零 NuGet，不能引 Wilson |
| RSA 金鑰 | `using var rsa = RSA.Create()` | 避免 RSA 物件洩漏 |
| EF 樂觀併發 | `IsConcurrencyToken()` + `RowVersion byte[]` 欄位（Infrastructure Shadow Property） | `IsRowVersion()` 是 SQL Server 專有，SQLite 不支援 |
| Port 綁定 | `launchSettings.json` 各設 `applicationUrl` | `dotnet run` 預設 Port 隨機，Gateway ocelot.json 需固定 5001/5002 |
| RT 重用偵測 | `TokenReuseException` → Controller 回 401 | 與 `UnauthorizedAccessException` 區分，Controller catch 分支清晰 |

---

## 驗收標準

執行 `pwsh scripts/test-flow.ps1` 全部 PASS：

| 測試 | 預期結果 |
|------|---------|
| POST `/api/token`（Dev Header 模擬 Windows 身分） | 200 + AT + RT |
| GET `/api/orders`（Bearer AT 走 Gateway） | 200，後端收到 X-User-Id |
| POST `/api/token/refresh` | 200 + 新 AT + 新 RT |
| 重用舊 RT（> 60 秒後） | 401 token_reuse_detected |
| 無效 AT 打 Gateway | 401 |
| 連續 21 次超過 Rate Limit | 429 |
