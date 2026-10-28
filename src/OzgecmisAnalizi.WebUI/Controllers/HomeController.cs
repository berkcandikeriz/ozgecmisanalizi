using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.WebUI.Models;

namespace OzgecmisAnalizi.WebUI.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();

    [HttpGet("/gizlilik")]
    public IActionResult Privacy() => View();

    [HttpGet("/hata")]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, StatusCode = 500 });

    [HttpGet("/durum/{code:int}")]
    public IActionResult Status(int code) => code == 404 ? View("NotFound") : View("Error", new ErrorViewModel { StatusCode = code, RequestId = HttpContext.TraceIdentifier });
}
