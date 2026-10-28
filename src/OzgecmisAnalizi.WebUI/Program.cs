using Microsoft.AspNetCore.Authentication.Cookies;
using OzgecmisAnalizi.Core.Logging;
using OzgecmisAnalizi.Core.Middleware;
using OzgecmisAnalizi.WebUI.Infrastructure;
using OzgecmisAnalizi.WebUI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddCoreLogging("ozgecmisanalizi-web");

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7101/");
        client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Api:TimeoutSeconds", 120));
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/hesap/giris";
        options.LogoutPath = "/hesap/cikis";
        options.AccessDeniedPath = "/hesap/giris";
        options.Cookie.Name = "ozgecmis.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCoreRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/durum/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
