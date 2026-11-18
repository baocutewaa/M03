using System.Diagnostics;
using M03_Opus.Models;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Controllers;

public sealed class HomeController(DashboardService dashboardService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await dashboardService.BuildAsync(User, ct).ConfigureAwait(false);
        return View(model);
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
