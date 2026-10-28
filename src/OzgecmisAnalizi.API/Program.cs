using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business;
using OzgecmisAnalizi.Core;
using OzgecmisAnalizi.Core.Logging;
using OzgecmisAnalizi.Core.Middleware;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Core.Security;
using OzgecmisAnalizi.DataAccess;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddCoreLogging("ozgecmisanalizi-api");

// --- Katmanlar
builder.Services.AddCoreServices();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusiness(builder.Configuration);

// --- Kimlik doğrulama (JWT)
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key en az 32 karakter olmalıdır. Geliştirmede appsettings.Development.json, üretimde ortam değişkeni (Jwt__Key) kullanın.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Bu işlem için giriş yapmalısınız.", null, context.HttpContext.TraceIdentifier));
            }
        };
    });
builder.Services.AddAuthorization();

// --- Kaba kuvvet koruması: giriş/kayıt uç noktaları IP başına dakikada 10 istek.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// --- CORS (Angular / React / Flutter web istemcileri için)
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition", CorrelationIdMiddleware.HeaderName)));

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationResponseFactory.Create);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCoreMiddlewares();
app.UseCoreRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("ÖzgeçmişAnalizi API"));
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect(app.Environment.IsDevelopment() ? "/scalar/v1" : "/health")).ExcludeFromDescription();

// Veritabanını oluştur/güncelle ve meslek kataloğunu yükle (code-first).
if (!app.Configuration.GetValue<bool>("Database:SkipMigrate"))
{
    await app.Services.MigrateAndSeedAsync();
}

await app.RunAsync();

/// <summary>Entegrasyon testleri için.</summary>
public partial class Program { }
