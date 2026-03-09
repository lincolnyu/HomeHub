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

    public void OnGet()
    {
       // NEW: Retrieve the same test file that was used during the test
        var testFilePath = HttpContext.Session.GetString(TestModel.TestFileSessionKey);

        TestConfig? config = null;
        if (!string.IsNullOrEmpty(testFilePath))
        {
            config = _service.LoadConfig(testFilePath);
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
            bool correct;

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

                correct = userList.SequenceEqual(correctList, StringComparer.OrdinalIgnoreCase);
            }
            else // single or text
            {
                correct = q.CorrectAnswers.Any(c =>
                    string.Equals(userAns.Trim(), c.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (correct) Score++;

            QuestionResults.Add(new QuestionResult(q, userAns, correct));
        }
    }

    public record QuestionResult(Question Question, string UserAnswer, bool IsCorrect);
}