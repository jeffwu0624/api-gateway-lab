using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models;

namespace WebApp.Controllers;

public class HomeController(IHttpClientFactory httpClientFactory) : Controller
{
    public IActionResult Index()
    {
        return View(new ApiResultViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CallApi()
    {
        var vm = new ApiResultViewModel();
        var client = httpClientFactory.CreateClient("Gateway");

        try
        {
            // Step 1: POST /api/token 取得 Token
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/token");
            tokenRequest.Headers.Add("X-Dev-Windows-User", @"CORP\jeff.wang");
            tokenRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { grant_type = "windows_identity" }),
                Encoding.UTF8, "application/json");

            var tokenResponse = await client.SendAsync(tokenRequest);
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
            vm.TokenJson = FormatJson(tokenBody);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                vm.ErrorMessage = $"Token 請求失敗: {tokenResponse.StatusCode}";
                return View("Index", vm);
            }

            // 解析 access_token
            using var tokenDoc = JsonDocument.Parse(tokenBody);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

            // Step 2: GET /api/orders 取得訂單
            var ordersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
            ordersRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var ordersResponse = await client.SendAsync(ordersRequest);
            var ordersBody = await ordersResponse.Content.ReadAsStringAsync();
            vm.OrdersJson = FormatJson(ordersBody);

            if (!ordersResponse.IsSuccessStatusCode)
                vm.ErrorMessage = $"Orders 請求失敗: {ordersResponse.StatusCode}";
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = ex is HttpRequestException
                ? "無法連線至 API Gateway，請確認服務已啟動"
                : "呼叫 API 時發生錯誤";
        }

        return View("Index", vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}
