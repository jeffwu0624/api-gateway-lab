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

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAccessTokenService, RsaAccessTokenService>();
builder.Services.AddScoped<IRefreshTokenGenerator, CryptoRefreshTokenGenerator>();
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
