using System.Text.Json;
using HomeHubApp.Pages.Naplan.Models;
using HomeHubApp.Pages.Naplan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.Naplan;

public class TestModel : PageModel
{
    private readonly IQuestionService _service;

    public TestModel(IQuestionService service)
    {
        _service = service;
    }

    [BindProperty(SupportsGet = true)] public int CurrentIndex { get; set; }

    [BindProperty] public string SelectedAnswer { get; set; } = string.Empty; // single + text

    [BindProperty] public List<string> SelectedMulti { get; set; } = new(); // multi

    public List<Question> Questions { get; private set; } = new();
    public Question CurrentQuestion { get; private set; } = new();
    public int TotalQuestions { get; private set; }

    // Add these properties
    public int? TotalTimeSeconds { get; private set; }
    public string? StartTimeUtcIso { get; private set; }
    public long ServerElapsedSeconds { get; private set; }

    public void OnGet(int index = 0)
    {
        // If starting at question 0 → treat as fresh attempt (from home or Try Again)
        if (index == 0)
        {
            // Clear previous timer and answers
            HttpContext.Session.Remove("TestStartUtcIso");
            HttpContext.Session.Remove("UserAnswers");

            // Optional: signal to JS to also clear localStorage
            ViewData["ForceReset"] = true;
        }

        LoadTestConfigAndSetCurrent(index);

        // Determine or create start time (only after possible clear)
        StartTimeUtcIso = HttpContext.Session.GetString("TestStartUtcIso");

        if (string.IsNullOrEmpty(StartTimeUtcIso))
        {
            StartTimeUtcIso = DateTime.UtcNow.ToString("o");
            HttpContext.Session.SetString("TestStartUtcIso", StartTimeUtcIso);
        }

        if (DateTime.TryParse(StartTimeUtcIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startUtc))
        {
            ServerElapsedSeconds = (long)(DateTime.UtcNow - startUtc).TotalSeconds;
        }
        else
        {
            ServerElapsedSeconds = 0;
        }

        PopulateSelectedAnswersFromSession();

        // Pass to view
        ViewData["StartTimeUtcIso"] = StartTimeUtcIso;
        ViewData["TotalTimeSeconds"] = TotalTimeSeconds ?? 0;
    }

    public IActionResult OnPostNext()
    {
        LoadTestConfigAndSetCurrent();
        SaveCurrentAnswer();
        var nextIndex = Math.Min(CurrentIndex + 1, TotalQuestions - 1);
        return RedirectToPage(new { index = nextIndex });
    }

    public IActionResult OnPostPrev()
    {
        LoadTestConfigAndSetCurrent();
        SaveCurrentAnswer();
        var prevIndex = Math.Max(CurrentIndex - 1, 0);
        return RedirectToPage(new { index = prevIndex });
    }

    public IActionResult OnPostSubmit()
    {
        LoadTestConfigAndSetCurrent();
        SaveCurrentAnswer();
        return RedirectToPage("Results");
    }

    // Optional: new handler to restore state from client (called by JS if needed)
    public IActionResult OnPostRestoreState(string answersJson, string startTimeIso)
    {
        if (!string.IsNullOrEmpty(answersJson))
        {
            HttpContext.Session.SetString("UserAnswers", answersJson);
        }
        if (!string.IsNullOrEmpty(startTimeIso))
        {
            HttpContext.Session.SetString("TestStartUtcIso", startTimeIso);
        }
        return new EmptyResult();
    }

    // ────────────────────────────────────────────────
    // Core loading logic – always sets CurrentQuestion
    // ────────────────────────────────────────────────
    private void LoadTestConfigAndSetCurrent(int? requestedIndex = null)
    {
        var config = _service.GetTestConfig();
        Questions = config.Questions ?? new List<Question>();
        TotalTimeSeconds = config.TotalTimeSeconds;
        TotalQuestions = Questions.Count;

        int targetIndex = requestedIndex ?? CurrentIndex;
        CurrentIndex = TotalQuestions == 0 ? 0 : Math.Clamp(targetIndex, 0, TotalQuestions - 1);

        CurrentQuestion = TotalQuestions > 0
            ? Questions[CurrentIndex]
            : new Question { Content = "No questions loaded. Please add naplan-questions.json" };
    }

    private void PopulateSelectedAnswersFromSession()
    {
        var answers = GetOrInitUserAnswers();
        var userAns = CurrentIndex < answers.Count ? answers[CurrentIndex] : "";

        if (CurrentQuestion.Type == "multi")
        {
            SelectedMulti = string.IsNullOrWhiteSpace(userAns)
                ? new List<string>()
                : userAns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            SelectedAnswer = "";
        }
        else
        {
            SelectedAnswer = userAns;
            SelectedMulti.Clear();
        }
    }

    private List<string> GetOrInitUserAnswers()
    {
        var json = HttpContext.Session.GetString("UserAnswers");
        if (string.IsNullOrEmpty(json))
        {
            var empty = Enumerable.Repeat("", TotalQuestions).ToList();
            SaveUserAnswers(empty);
            return empty;
        }

        var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        if (list.Count < TotalQuestions)
        {
            list.AddRange(Enumerable.Repeat("", TotalQuestions - list.Count));
            SaveUserAnswers(list);
        }

        return list;
    }

    private void SaveCurrentAnswer()
    {
        var answers = GetOrInitUserAnswers();
        if (CurrentIndex >= answers.Count) return;

        var toSave = CurrentQuestion.Type switch
        {
            "multi" => string.Join(",",
                SelectedMulti
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),

            _ => SelectedAnswer?.Trim() ?? ""
        };

        answers[CurrentIndex] = toSave;
        SaveUserAnswers(answers);
    }

    private void SaveUserAnswers(List<string> answers)
    {
        HttpContext.Session.SetString("UserAnswers", JsonSerializer.Serialize(answers));
    }
}