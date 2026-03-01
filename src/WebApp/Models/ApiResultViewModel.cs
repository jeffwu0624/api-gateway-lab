namespace WebApp.Models;

public class ApiResultViewModel
{
    public string? TokenJson { get; set; }
    public string? OrdersJson { get; set; }
    public string? RefreshJson { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasResult => TokenJson != null || OrdersJson != null || RefreshJson != null || ErrorMessage != null;
}
