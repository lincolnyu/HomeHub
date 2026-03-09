using System.Text.Json;
using HomeHubApp.Pages.Naplan.Models;
using HomeHubApp.Pages.Naplan.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.Naplan;

public class ResultsModel : PageModel
{
    private readonly IQuestionService _service;

    public ResultsModel(IQuestionService service)
    {
        _service = service;
    }

    public int Score { get; set; }
    public int Total { get; set; }
    public List<QuestionResult> QuestionResults { get; set; } = new();
    public string TimeUsedDisplay { get; set; } = "—";
    public string TimeAllowedDisplay { get; set; } = null; // null = no limit

    public void OnGet()
    {
        // NEW: Retrieve the same test file that was used during the test
        var testFilePath = HttpContext.Session.GetString(TestModel.TestFileSessionKey);

        TestConfig? config = null;
        if (!string.IsNullOrEmpty(testFilePath))
        {
            config = _service.LoadConfig(testFilePath);
        }

        // Calculate time used
        var startIso = HttpContext.Session.GetString("TestStartUtcIso");
        if (!string.IsNullOrEmpty(startIso) &&
            DateTime.TryParse(startIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startUtc))
        {
            var elapsed = DateTime.UtcNow - startUtc;
            var elapsedSeconds = (long)elapsed.TotalSeconds;

            // Cap at allowed time if limit was set
            if (config?.TotalTimeSeconds > 0)
            {
                elapsedSeconds = Math.Min(elapsedSeconds, config.TotalTimeSeconds.Value);
            }

            var minutes = elapsedSeconds / 60;
            var seconds = elapsedSeconds % 60;
            TimeUsedDisplay = $"{minutes:D2}:{seconds:D2}";

            // Time allowed (if any)
            if (config?.TotalTimeSeconds > 0)
            {
                var allowedMinutes = config.TotalTimeSeconds.Value / 60;
                var allowedSeconds = config.TotalTimeSeconds.Value % 60;
                TimeAllowedDisplay = $"{allowedMinutes:D2}:{allowedSeconds:D2}";
            }
        }

        var questions = config?.Questions ?? new List<Question>();

        // Optional: filter to real (non-informational) questions only
        var realQuestions = questions
            .Where(q => !string.IsNullOrEmpty(q.Type) && q.Type != "none")
            .ToList();

        Total = realQuestions.Count;
        if (Total == 0) return;

        var json = HttpContext.Session.GetString(TestModel.UserAnswersSessionKey);
        var userAnswers = string.IsNullOrEmpty(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

        Score = 0;

        for (var i = 0; i < Total; i++)
        {
            var q = realQuestions[i];
            var userAns = i < userAnswers.Count ? userAnswers[i] : "";
            bool correct = IfQuestionAnsweredCorrectly(q, userAns);
        
            if (correct) Score++;

            QuestionResults.Add(new QuestionResult(q, userAns, correct));
        }
    }

    public static bool IfQuestionAnsweredCorrectly(Question q, string userAns)
    {
        if (q.Type == "multi")
        {
            var userList = string.IsNullOrEmpty(userAns)
                ? new List<string>()
                : userAns.Split(',')
                         .Select(x => x.Trim())
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                         .ToList();

            var correctList = q.CorrectAnswers
                               .Select(x => x.Trim())
                               .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                               .ToList();

            return userList.SequenceEqual(correctList, StringComparer.OrdinalIgnoreCase);
        }
        else // single or text
        {
            return q.CorrectAnswers.Any(c =>
                string.Equals(userAns.Trim(), c.Trim(), StringComparison.OrdinalIgnoreCase));
        }

    }

    public record QuestionResult(Question Question, string UserAnswer, bool IsCorrect);
}