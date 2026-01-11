using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.LearningChineseWithPinyin
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public required string InputText { get; set; }

        public required string OutputText { get; set; }

        public void OnGet()
        {
            // Initial page load - no action needed
        }

        public void OnPost()
        {
            if (!string.IsNullOrEmpty(InputText))
            {
                // Placeholder: Call your conversion logic here (implement the method below or replace with your library/program call)
                OutputText = PinyinGenerator.ConvertToPinyin(InputText, 32, (13,-1));
            }
        }

        public IActionResult OnGetSample()
        {
            InputText = "鹅，鹅，鹅\n曲项向天歌\n白毛浮绿水\n红掌拨清波";

            OnPost();   // ← would call your conversion right away
            return Page();
        }

      
    }
}