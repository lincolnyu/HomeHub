using System.Text.Json;
using HomeHubApp.Pages.Naplan.Models;

namespace HomeHubApp.Pages.Naplan.Services;

public class QuestionService : IQuestionService
{
    private readonly TestConfig _config = new();

    public QuestionService(IWebHostEnvironment environment)
    {
        try
        {
            var filePath = Path.Combine(environment.WebRootPath, "data", "naplan-questions.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: naplan-questions.json not found at {filePath}");
                return;
            }

            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Try to deserialize as new object format first
            var tempConfig = JsonSerializer.Deserialize<TestConfig>(json, options);
            if (tempConfig?.Questions?.Any() == true)
            {
                _config = tempConfig;
            }
            else
            {
                // Fallback: old array format
                var questions = JsonSerializer.Deserialize<List<Question>>(json, options) ?? new();
                _config.Questions = questions;
                _config.TotalTimeSeconds = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading naplan-questions.json: {ex.Message}");
        }
    }

    public TestConfig GetTestConfig() => _config;
}