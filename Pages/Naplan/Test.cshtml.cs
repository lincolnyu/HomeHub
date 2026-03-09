using System.Text.Json;
using HomeHubApp.Pages.Naplan.Models;
using HomeHubApp.Pages.Naplan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.Naplan;

public class TestModel : PageModel
{
    private readonly IQuestionService _service;

    public const string TestFileSessionKey = "CurrentTestFilePath";
    public const string UserAnswersSessionKey = "UserAnswers";
    public const string TestStartSessoinKey = "TestStartUtcIso";

    public TestModel(IQuestionService service)
    {
        _service = service;
    }

    [BindProperty(SupportsGet = true)] public int CurrentPageIndex { get; set; }

    [BindProperty] public string SelectedAnswer { get; set; } = string.Empty; // single + text

    [BindProperty] public List<string> SelectedMulti { get; set; } = new(); // multi

    public List<Question> QuestionPages { get; private set; } = new();
    public Question CurrentPage { get; private set; } = new();
    public int TotalPages { get; private set; }

    public int? TotalTimeSeconds { get; private set; }
    public string? StartTimeUtcIso { get; private set; }
    public long ServerElapsedSeconds { get; private set; }

    public int VisibleQuestionCount { get; private set; }
    public int VisibleQuestionNumber { get; private set; }   // 1-based among real questions
    public int? ParentIndex { get; private set; }            // flat index of parent if any

    public void OnGet(int index = 0)
    {
        bool isRetry = HttpContext.Request.Query["retry"] == "true";

        // ────────────────────────────────────────────────
        // NEW: Decide whether to pick a new random test file
        // ────────────────────────────────────────────────
        if (index == 0 && !isRetry)
        {
            // True fresh start (from home) → force new random selection
            HttpContext.Session.Remove(TestFileSessionKey);
        }

        // If no test file selected yet → choose one randomly
        var selectedPath = HttpContext.Session.GetString(TestFileSessionKey);
        if (string.IsNullOrEmpty(selectedPath))
        {
            var allFiles = _service.GetAllTestFilePaths();
            if (allFiles.Any())
            {
                var rnd = new Random();
                selectedPath = allFiles[rnd.Next(allFiles.Count)];
                HttpContext.Session.SetString(TestFileSessionKey, selectedPath);

                // Log to console (as requested)
                Console.WriteLine($"[NAPLAN] Selected test file for this session: {Path.GetFileName(selectedPath)}");
            }
            else
            {
                Console.WriteLine("[NAPLAN] No test files found on server");
            }
        }

        // ────────────────────────────────────────────────
        // Fresh start cleanup (always clear answers & timer)
        // ────────────────────────────────────────────────
        if (index == 0)
        {
            // Clear previous timer and answers
            HttpContext.Session.Remove(TestStartSessoinKey);
            HttpContext.Session.Remove(UserAnswersSessionKey);

            // Optional: signal to JS to also clear localStorage
            ViewData["ForceReset"] = true;
        }

        LoadTestConfigAndSetCurrent(index);

        // Determine or create start time (only after possible clear)
        StartTimeUtcIso = HttpContext.Session.GetString(TestStartSessoinKey);

        if (string.IsNullOrEmpty(StartTimeUtcIso))
        {
            StartTimeUtcIso = DateTime.UtcNow.ToString("o");
            HttpContext.Session.SetString(TestStartSessoinKey, StartTimeUtcIso);
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

        ViewData["StartTimeUtcIso"] = StartTimeUtcIso;
        ViewData["TotalTimeSeconds"] = TotalTimeSeconds ?? 0;
    }

    public IActionResult OnPostNext()
    {
        LoadTestConfigAndSetCurrent();
        SaveCurrentAnswer();
        var nextIndex = Math.Min(CurrentPageIndex + 1, TotalPages - 1);
        return RedirectToPage(new { index = nextIndex });
    }

    public IActionResult OnPostPrev()
    {
        LoadTestConfigAndSetCurrent();
        SaveCurrentAnswer();
        var prevIndex = Math.Max(CurrentPageIndex - 1, 0);
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
            HttpContext.Session.SetString(UserAnswersSessionKey, answersJson);
        }
        if (!string.IsNullOrEmpty(startTimeIso))
        {
            HttpContext.Session.SetString(TestStartSessoinKey, startTimeIso);
        }
        return new EmptyResult();
    }

    // ────────────────────────────────────────────────
    // Core loading logic – always sets CurrentPage
    // ────────────────────────────────────────────────
    private void LoadTestConfigAndSetCurrent(int? requestedIndex = null)
    {
        var path = HttpContext.Session.GetString(TestFileSessionKey);

        TestConfig? config = null;
        if (!string.IsNullOrEmpty(path))
        {
            config = _service.LoadConfig(path);
        }

        QuestionPages = config?.Questions ?? new List<Question>();
        TotalTimeSeconds = config?.TotalTimeSeconds;
        TotalPages = QuestionPages.Count;

        int targetIndex = requestedIndex ?? CurrentPageIndex;
        CurrentPageIndex = TotalPages == 0 ? 0 : Math.Clamp(targetIndex, 0, TotalPages - 1);

        // Check server /wwwroot/data/naplan
        CurrentPage = TotalPages > 0
            ? QuestionPages[CurrentPageIndex]
            : new Question { Content = "No test loaded. Report it to admin." };

        // ────────────────────────────────────────────────
        // NEW: Calculate visible (real) questions
        // ────────────────────────────────────────────────
        List<Question> visibleQuestions = GetVisibleQuestions();

        VisibleQuestionCount = visibleQuestions.Count;

        // Questions up to this page.
        VisibleQuestionNumber = GetNumberOfQuestionsBeforeThisPage(visibleQuestions, CurrentPageIndex); 
        
        // Parent index (flat list position)
        var parent = QuestionPages.FirstOrDefault(q => q.Id == CurrentPage.ParentId);
        ParentIndex = parent != null ? QuestionPages.IndexOf(parent) : null;
    }

    private int GetNumberOfQuestionsBeforeThisPage(List<Question> visibleQuestions, int pageIndex)
    {
        for(; pageIndex >= 0 ; pageIndex--)
        {
            var page = QuestionPages[pageIndex];
            var visibleQuestionIndex = visibleQuestions.IndexOf(page);
            if (visibleQuestionIndex >= 0)
            {
                return visibleQuestionIndex + 1;
            }
        }
        return 0;
    }

    private void PopulateSelectedAnswersFromSession()
    {
        var answers = GetOrInitUserAnswers();
        var visibleIndex = GetVisibleIndexForCurrentPage();
        var userAns = visibleIndex >= 0 && visibleIndex < answers.Count
            ? answers[visibleIndex]
            : "";

        if (CurrentPage.Type == "multi")
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
        var json = HttpContext.Session.GetString(UserAnswersSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            // NEW: only allocate slots for real questions
            var empty = Enumerable.Repeat("", VisibleQuestionCount).ToList();
            SaveUserAnswers(empty);
            return empty;
        }

        var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        if (list.Count < VisibleQuestionCount)
        {
            list.AddRange(Enumerable.Repeat("", VisibleQuestionCount - list.Count));
            SaveUserAnswers(list);
        }

        return list;
    }

    private void SaveCurrentAnswer()
    {
        // Skip if informational (no answer needed/saved)
        if (string.IsNullOrEmpty(CurrentPage.Type) || CurrentPage.Type == "none")
            return;

        var visibleIndex = GetVisibleIndexForCurrentPage();
        if (visibleIndex < 0) return; // safety

        var answers = GetOrInitUserAnswers();
        if (visibleIndex >= answers.Count) return;

        var toSave = CurrentPage.Type switch
        {
            "multi" => string.Join(",",
                SelectedMulti
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),

            _ => SelectedAnswer?.Trim() ?? ""
        };

        answers[visibleIndex] = toSave;
        SaveUserAnswers(answers);
    }

    private int GetVisibleIndexForCurrentPage()
    {
        if (string.IsNullOrEmpty(CurrentPage.Type) || CurrentPage.Type == "none")
            return -1; // informational → no answer slot

        var visibleQuestions = GetVisibleQuestions();

        int visibleIndex = visibleQuestions.IndexOf(CurrentPage);
        return visibleIndex >= 0 ? visibleIndex : -1;
    }

    private List<Question> GetVisibleQuestions()
    {
        return QuestionPages
            .Where(q => !string.IsNullOrEmpty(q.Type) && q.Type != "none")
            .ToList();
    }

    private void SaveUserAnswers(List<string> answers)
    {
        HttpContext.Session.SetString(UserAnswersSessionKey, JsonSerializer.Serialize(answers));
    }
}