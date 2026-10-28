using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.WebUI.Infrastructure;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

/// <summary>Oturum gerektiren sayfaların tabanı.</summary>
[Authorize]
public abstract class AppController(ApiClient api) : Controller
{
    protected ApiClient Api { get; } = api;

    protected void Success(string message) => TempData[Alerts.Success] = message;
    protected void Error(string message) => TempData[Alerts.Error] = message;
    protected void Info(string message) => TempData[Alerts.Info] = message;

    protected IActionResult PdfFile(FileResultData file) => File(file.Content, file.ContentType, file.FileName);
}
