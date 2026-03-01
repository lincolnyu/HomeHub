using System.Text.Json;
using HomeHubApp.Pages.Naplan.Models;

namespace HomeHubApp.Pages.Naplan.Services;

public class QuestionService : IQuestionService
{
    private readonly List<Question> _questions = new();

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
            _questions = JsonSerializer.Deserialize<List<Question>>(json, options) ?? new List<Question>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading naplan-questions.json: {ex.Message}");
            // Fallback to empty list – site still runs
        }
    }

    public List<Question> GetQuestions()
    {
        return _questions;
    }
}