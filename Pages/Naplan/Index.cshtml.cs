    using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.Naplan
{
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
            HttpContext.Session.Remove("UserAnswers");
            HttpContext.Session.Remove("TestStartUtcIso");
        }
    }
}
