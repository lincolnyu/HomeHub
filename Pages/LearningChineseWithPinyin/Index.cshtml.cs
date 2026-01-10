using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.LearningChineseWithPinyin
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string InputText { get; set; }

        public string OutputText { get; set; }

        public void OnGet()
        {
            // Initial page load - no action needed
        }

        public void OnPost()
        {
            if (!string.IsNullOrEmpty(InputText))
            {
                // Placeholder: Call your conversion logic here (implement the method below or replace with your library/program call)
                OutputText = ConvertToPinyin(InputText);
            }
        }

        private string ConvertToPinyin(string input)
        {
            // Implement your conversion logic here.
            // This should process the input Chinese text and return the adorned string,
            // with pinyin on every first line and Chinese on every second line.
            // For example, use your provided program/library to generate the output.
            return "Placeholder pinyin line\nPlaceholder Chinese line"; // Replace with actual implementation
        }
    }
}