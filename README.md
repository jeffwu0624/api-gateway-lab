# API Gateway Lab

本機重現「公司內部員工瀏覽器 → API Gateway → 後端 API」完整 JWT 驗證流程。使用 Dev Header 模擬 Windows 身分，無需 AD/IIS 環境即可開發與測試。包含 Token Service（Clean Architecture 四層）、Ocelot API Gateway、SampleApi 三個服務。

## 前置需求

- .NET 8 SDK（或以上）
- PowerShell（Windows 內建即可）

## 快速啟動

```bash
# 1. 產生 RSA 金鑰（首次執行）
powershell -ExecutionPolicy Bypass -File scripts/generate-rsa-keys.ps1

# 2. 建置整個 Solution
dotnet build ApiGatewayLab.sln

# 3. 啟動服務（三個終端機）
cd src/TokenService/TokenService.API && dotnet run   # :5001
cd src/ApiGateway                   && dotnet run   # :5000
cd src/SampleApi                    && dotnet run   # :5002
```

TokenService 啟動時會自動執行 EF Migration 與 Seed（建立測試使用者 `jeff.wang` / `alice.chen`）。

## 執行測試

```bash
# 單元測試
dotnet test tests/TokenService.Application.Tests

# 端對端測試（需先啟動三個服務）
powershell -ExecutionPolicy Bypass -File scripts/test-flow.ps1
```

## 已知限制

- **Dev Mock**：使用 `X-Dev-Windows-User` Header 模擬 Windows 身分，非實際 AD 驗證
- **SQLite**：本機 Lab 用 SQLite，不支援部分 SQL Server 功能（如 `IsRowVersion()`）
- **HTTP only**：本機開發不啟用 HTTPS

## 生產環境對應

| Lab 設計 | 生產環境替換 |
|---------|------------|
| `X-Dev-Windows-User` Header | IIS In-Process + Negotiate 驗證 |
| SQLite `app.db` | Oracle DB + ODP.NET |
| `Keys/private.pem` 本機檔案 | Azure Key Vault |
| HTTP | HTTPS + TLS 1.2+ + HSTS |
| `dotnet run` 三個終端機 | IIS / Windows Service |
