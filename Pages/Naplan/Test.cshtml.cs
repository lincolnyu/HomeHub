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

    [BindProperty] public string SelectedAnswer { get; set; } = string.Empty; // For single/text

    [BindProperty] public List<string> SelectedMulti { get; set; } = new(); // For multi

    public List<Question> Questions { get; private set; } = new();
    public Question CurrentQuestion { get; private set; } = new();
    public int TotalQuestions { get; private set; }

    public void OnGet(int index = 0)
    {
        LoadQuestions(index);

        var answers = GetOrInitUserAnswers();
        var userAns = answers.Count > CurrentIndex ? answers[CurrentIndex] : "";

        if (CurrentQuestion.Type == "multi")
        {
            SelectedMulti = string.IsNullOrEmpty(userAns)
                ? new List<string>()
                : userAns.Split(',').Select(x => x.Trim()).ToList();
            SelectedAnswer = "";
        }
        else
        {
            SelectedAnswer = userAns;
            SelectedMulti = new List<string>();
        }
    }

    public IActionResult OnPostNext()
    {
        LoadQuestions();
        SaveCurrentAnswer();
        var next = Math.Min(CurrentIndex + 1, TotalQuestions - 1);
        return RedirectToPage(new { index = next });
    }

    public IActionResult OnPostPrev()
    {
        LoadQuestions();
        SaveCurrentAnswer();
        var prev = Math.Max(CurrentIndex - 1, 0);
        return RedirectToPage(new { index = prev });
    }

    public IActionResult OnPostSubmit()
    {
        LoadQuestions();
        SaveCurrentAnswer();
        return RedirectToPage("Results");
    }

    private void LoadQuestions(int? index = null)
    {
        Questions = _service.GetQuestions();
        TotalQuestions = Questions.Count;

        if (index is not null) CurrentIndex = Math.Clamp(index.Value, 0, TotalQuestions - 1);
        // Graceful fallback
        CurrentQuestion = TotalQuestions == 0 ? new Question { Content = "No questions loaded. Please add naplan-questions.json" } : Questions[CurrentIndex];
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
        if (list.Count != TotalQuestions)
        {
            list = list.Concat(Enumerable.Repeat("", TotalQuestions - list.Count)).ToList();
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
            "multi" => string.Join(",", SelectedMulti.OrderBy(x => x)),
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