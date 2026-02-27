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

RsaSecurityKey rsaKey;
using (var rsa = RSA.Create())
{
    rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
    rsaKey = new RsaSecurityKey(rsa.ExportParameters(false));
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = rsaKey,
            ClockSkew = TimeSpan.FromSeconds(10)
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
app.UseRateLimiter();
await app.UseOcelot();
app.Run();
