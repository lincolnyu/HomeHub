using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.Arithmentic;

public class ArithmenticModel : PageModel
{
    private static Game? _game;
    private static List<Answer> _loggedAnswers = [];
    private static string? _lastMessage;
    private static bool _gameStarted = false;
    private static bool _isFinished = false;

    // Config values exposed to the view
    public int DigitsOperand1 => _game?.DigitsOperand1 ?? 0;
    public int DigitsOperand2 => _game?.DigitsOperand2 ?? 0;
    public double MinAnswersPerMinRequired => _game?.MinAnswersPerMinRequired ?? 0;
    public int ConsecutiveSuccessesRequired => _game?.ConsecutiveSuccessesRequired ?? 0;
    public int NonRepeatQueueLength => _game?.NonRepeatQueueLength ?? 0;
    public int ReinforceRepeatCap => _game?.ReinforceRepeatCap ?? 0;
    public string? LogFilePath => _game?.LogFilePath;

    public bool GameStarted => _gameStarted;
    public bool IsFinished => _isFinished;
    public string? LastMessage => _lastMessage;
    public List<Answer> LoggedAnswers => _loggedAnswers;

    public (int Operand1, int Operand2)? CurrentQuestion => _game?.CurrentQuestion;
    public int CurrentOperand1 => CurrentQuestion?.Operand1 ?? 0;
    public int CurrentOperand2 => CurrentQuestion?.Operand2 ?? 0;

    public (double perfRatio, int op1, int op2)? MaxPerf => _game?.MaxPerf;

    public double? MaxPerfReport => _game?.MaxPerf.HasValue == true ? Answer.PerformanceRatioToReportRatio(_game.MaxPerf.Value.perfRatio) : null;
    public string? MaxPerfQuestion => _game?.MaxPerf.HasValue == true ? $"{_game.MaxPerf.Value.op1}×{_game.MaxPerf.Value.op2}" : null;
    public double? MinPerfReport => _game?.MinPerf.HasValue == true ? Answer.PerformanceRatioToReportRatio(_game.MinPerf.Value.perfRatio) : null;
    public string? MinPerfQuestion => _game?.MinPerf.HasValue == true ? $"{_game.MinPerf.Value.op1}×{_game.MinPerf.Value.op2}" : null;
    public int ConsecutiveSuccess => _game?.ConsecutiveSuccess ?? 0;

    public void OnGet()
    {
        if (_game == null)
        {
            _game = Game.CreateFromConfig(@"Pages\Arithmentic\multiplier.cfg");
        }
    }

    public IActionResult OnPostStart()
    {
        _gameStarted = true;
        _isFinished = false;
        _loggedAnswers.Clear();
        _lastMessage = null;
        _game!.StartSession();
        return RedirectToPage();
    }

    public IActionResult OnPostAnswer(string userAnswer, long startTicks)
    {
        var startTime = new DateTime(startTicks);
        var timeUsed = DateTime.Now - startTime;

        var result = _game!.ProcessAnswer(userAnswer, timeUsed, out _lastMessage);

        if (result.logEntry is not null)
            _loggedAnswers.Add(result.logEntry);

        if (result.isFinished)
        {
            _isFinished = true;
            _gameStarted = false;
            _game.SaveLogIfEnabled();
        }

        return RedirectToPage();
    }

    public IActionResult OnGetDownloadLog()
    {
        if (string.IsNullOrEmpty(_game?.LogFilePath) || !System.IO.File.Exists(_game.LogFilePath))
            return NotFound();

        var bytes = System.IO.File.ReadAllBytes(_game.LogFilePath);
        return File(bytes, "text/plain", System.IO.Path.GetFileName(_game.LogFilePath));
    }
}
