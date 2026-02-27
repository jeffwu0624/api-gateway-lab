namespace TokenService.API.Middleware;

public class DevWindowsAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var devUser = ctx.Request.Headers["X-Dev-Windows-User"].FirstOrDefault();
        if (!string.IsNullOrEmpty(devUser))
            ctx.Items["WindowsUser"] = devUser;
        await next(ctx);
    }
}
