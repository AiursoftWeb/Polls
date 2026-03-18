using Aiursoft.Polls.Models.HomeViewModels;
using Microsoft.AspNetCore.Mvc;
using Aiursoft.WebTools.Attributes;
using Aiursoft.Polls.Services;

namespace Aiursoft.Polls.Controllers;

[LimitPerMin]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        
        var model = new IndexViewModel();
        return this.SimpleView(model);
    }
}
