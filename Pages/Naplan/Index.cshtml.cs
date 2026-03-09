using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.Naplan
{
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
            HttpContext.Session.Remove(TestModel.UserAnswersSessionKey);
            HttpContext.Session.Remove(TestModel.TestStartSessoinKey);
        }
    }
}
