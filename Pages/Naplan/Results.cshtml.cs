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
        var questions = _service.GetQuestions();
        Total = questions.Count;
        if (Total == 0) return;

        var json = HttpContext.Session.GetString("UserAnswers");
        var userAnswers = string.IsNullOrEmpty(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

        Score = 0;

        for (var i = 0; i < Total; i++)
        {
            var q = questions[i];
            var userAns = i < userAnswers.Count ? userAnswers[i] : "";
            var correct = string.Equals(userAns, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase);

            if (correct) Score++;

            QuestionResults.Add(new QuestionResult(q, userAns, correct));
        }
    }

    public record QuestionResult(Question Question, string UserAnswer, bool IsCorrect);
}