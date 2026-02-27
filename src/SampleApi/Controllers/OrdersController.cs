using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

[ApiController, Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpGet]
    public IActionResult GetOrders()
    {
        var userId = Request.Headers["X-User-Id"].ToString();
        var userRoles = Request.Headers["X-User-Roles"].ToString();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Missing X-User-Id" });

        return Ok(new
        {
            requestedBy = userId,
            roles = userRoles,
            orders = new[]
            {
                new { id = 1, product = "Widget A", qty = 10 },
                new { id = 2, product = "Widget B", qty = 5 }
            }
        });
    }
}
