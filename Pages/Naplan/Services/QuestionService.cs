using System.Text.Json;
using HomeHubApp.Pages.Naplan.Models;

namespace HomeHubApp.Pages.Naplan.Services;

public class QuestionService : IQuestionService
{
    public IWebHostEnvironment Environment { get; }

    public QuestionService(IWebHostEnvironment environment)
    {
        Environment = environment;
    }

    public List<string> GetAllTestFilePaths()
    {
        var dir = Path.Combine(Environment.WebRootPath, "data", "naplan", "input");

        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"Directory not found: {dir}");
            return new List<string>();
        }

        var files = Directory.GetFiles(dir, "test*.json", SearchOption.TopDirectoryOnly)
                             .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                             .ToList();

        return files;
    }

    public TestConfig? LoadConfig(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"File not found: {fullPath}");
            return null;
        }

        try
        {
            var json = File.ReadAllText(fullPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var tempConfig = JsonSerializer.Deserialize<TestConfig>(json, options);
            if (tempConfig?.Questions?.Any() == true)
            {
                return tempConfig;
            }

            // Fallback: old array format
            var questions = JsonSerializer.Deserialize<List<Question>>(json, options) ?? new();
            return new TestConfig
            {
                Questions = questions,
                TotalTimeSeconds = null
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading {fullPath}: {ex.Message}");
            return null;
        }
    }
}