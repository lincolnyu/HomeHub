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

    public void OnGet(int index = 0)
    {
        LoadQuestionsAndSetCurrent(index);
        PopulateSelectedAnswersFromSession();
    }

    public IActionResult OnPostNext()
    {
        LoadQuestionsAndSetCurrent();
        SaveCurrentAnswer();
        var nextIndex = Math.Min(CurrentIndex + 1, TotalQuestions - 1);
        return RedirectToPage(new { index = nextIndex });
    }

    public IActionResult OnPostPrev()
    {
        LoadQuestionsAndSetCurrent();
        SaveCurrentAnswer();
        var prevIndex = Math.Max(CurrentIndex - 1, 0);
        return RedirectToPage(new { index = prevIndex });
    }

    public IActionResult OnPostSubmit()
    {
        LoadQuestionsAndSetCurrent();
        SaveCurrentAnswer();
        return RedirectToPage("Results");
    }

    // ────────────────────────────────────────────────
    // Core loading logic – always sets CurrentQuestion
    // ────────────────────────────────────────────────
    private void LoadQuestionsAndSetCurrent(int? requestedIndex = null)
    {
        Questions = _service.GetQuestions() ?? new List<Question>();
        TotalQuestions = Questions.Count;

        // Determine final index
        var targetIndex = requestedIndex ?? CurrentIndex;
        CurrentIndex = TotalQuestions == 0 ? 0 : Math.Clamp(targetIndex, 0, TotalQuestions - 1);

        // Set current question (safe even when no questions)
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