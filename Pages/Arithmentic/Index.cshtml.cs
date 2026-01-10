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

// ====================== FULL PORT OF YOUR ORIGINAL LOGIC ======================
public class Answer
{
    public (int, int) Operands { get; }
    public int AnswerValue { get; }
    public bool IsCorrect => Operands.Item1 * Operands.Item2 == AnswerValue;
    public TimeSpan TimeSpent { get; }
    public double PerformanceRatio { get; }

    public Answer((int, int) operands, int answer, TimeSpan timeSpent, double performanceRatio)
    {
        Operands = operands;
        AnswerValue = answer;
        TimeSpent = timeSpent;
        PerformanceRatio = performanceRatio;
    }

    public static double PerformanceRatioToReportRatio(double performanceRatio)
        => (performanceRatio - 1) * 100.0;

    public override string ToString()
    {
        var sign = IsCorrect ? "=" : "≠";
        var reportRatio = PerformanceRatioToReportRatio(PerformanceRatio);
        return $"{Operands.Item1}×{Operands.Item2}{sign}{AnswerValue},{TimeSpent.TotalSeconds:F2}s,{reportRatio:F2}%";
    }
}

internal class Game
{
    public static Game CreateFromConfig(string configPath)
    {
        // Exact same parsing logic as your original program
        string[] lines = System.IO.File.ReadAllLines(configPath);
        var digitsOperand1 = int.Parse(lines[0]);
        var digitsOperand2 = int.Parse(lines[1]);
        var answersPerMin = double.Parse(lines[2]);
        var consecutiveCorrect = int.Parse(lines[3]);
        var loggerFilePath = lines.Length > 4 && !string.IsNullOrWhiteSpace(lines[4]) ? lines[4] : null;

        var (snc, sc, mnc, mc) = (0.9, 2.0, 2.1, 2.8);
        if (lines.Length > 5 && !string.IsNullOrWhiteSpace(lines[5]))
        {
            var coeffs = lines[5].Split(',');
            // same parsing logic as original (supports 1–4 values)
            if (coeffs.Length == 1) { var v = double.Parse(coeffs[0]); snc = sc = mnc = mc = v; }
            else if (coeffs.Length == 2) { snc = double.Parse(coeffs[0]); sc = mnc = mc = double.Parse(coeffs[1]); }
            else if (coeffs.Length == 3) { snc = double.Parse(coeffs[0]); sc = double.Parse(coeffs[1]); mnc = double.Parse(coeffs[2]); mc = snc * (mnc / snc); }
            else if (coeffs.Length >= 4) { sc = double.Parse(coeffs[0]); snc = double.Parse(coeffs[1]); mc = double.Parse(coeffs[2]); mnc = double.Parse(coeffs[3]); }
        }

        var nonRepeat = lines.Length > 6 ? int.Parse(lines[6]) : 3;
        var reinforceCap = lines.Length > 7 ? int.Parse(lines[7]) : 5;

        return new Game(digitsOperand1, digitsOperand2, answersPerMin, consecutiveCorrect,
                       (snc, sc, mnc, mc), nonRepeat, reinforceCap, loggerFilePath);
    }

    // ... (all the fields and methods from your original Game class - pasted verbatim below) ...

    public int DigitsOperand1 { get; }
    public int DigitsOperand2 { get; }
    public double MinAnswersPerMinRequired { get; }
    public int ConsecutiveSuccessesRequired { get; }
    public (double singleNoCarry, double singleCarry, double multiNoCarry, double multiCarry) AdditionCoeff { get; }
    public int NonRepeatQueueLength { get; }
    public int ReinforceRepeatCap { get; }
    public string? LogFilePath { get; }

    private readonly Dictionary<(int, int), int> _pastErrors = [];
    private readonly Dictionary<(int, int), int> _pastSlowAnswers = [];
    private readonly Dictionary<(int, int), int> _pastMaxWeakness = [];
    private readonly Queue<(int, int)> _previousOperands = new();
    private readonly Random _rand = new();
    private readonly TimeSpan _referenceSingleDigitTime;

    public (int Operand1, int Operand2)? CurrentQuestion { get; private set; }
    public int ConsecutiveSuccess { get; private set; }
    public (double perfRatio, int op1, int op2)? MaxPerf { get; private set; }
    public (double perfRatio, int op1, int op2)? MinPerf { get; private set; }

    private Game(int d1, int d2, double apm, int cons, (double snc, double sc, double mnc, double mc) coeff,
                 int nonRepeat, int reinforceCap, string? logPath)
    {
        DigitsOperand1 = d1; DigitsOperand2 = d2; MinAnswersPerMinRequired = apm;
        ConsecutiveSuccessesRequired = cons; AdditionCoeff = coeff;
        NonRepeatQueueLength = nonRepeat; ReinforceRepeatCap = reinforceCap;
        LogFilePath = logPath;
        _referenceSingleDigitTime = TimeSpan.FromSeconds(60.0 / apm);
    }

    public void StartSession()
    {
        _pastErrors.Clear(); _pastSlowAnswers.Clear(); _pastMaxWeakness.Clear();
        _previousOperands.Clear();
        ConsecutiveSuccess = 0;
        MaxPerf = MinPerf = null;
        GenerateNextQuestion();
    }

    private void GenerateNextQuestion()
    {
    regenerate:
        var (op1, op2) = GenerateOperands();
        var ordered = OrderOperands((op1, op2));
        if (_previousOperands.Contains(ordered)) goto regenerate;

        _previousOperands.Enqueue(ordered);
        if (_previousOperands.Count > NonRepeatQueueLength) _previousOperands.Dequeue();

        CurrentQuestion = (op1, op2);
    }

    public (bool isFinished, Answer? logEntry) ProcessAnswer(string input, TimeSpan timeUsed, out string message)
    {
        message = "";
        if (!int.TryParse(input, out var parsedAnswer))
            parsedAnswer = -999999;

        var (op1, op2) = CurrentQuestion!.Value;
        var correct = op1 * op2;
        var ordered = OrderOperands((op1, op2));

        var allowedTime = _referenceSingleDigitTime * AssessComplexity(op1, op2, AdditionCoeff);
        var perfRatio = allowedTime.TotalSeconds / timeUsed.TotalSeconds;

        var logEntry = new Answer((op1, op2), parsedAnswer, timeUsed, perfRatio);

        if (parsedAnswer == correct)
        {
            bool withinTime = timeUsed <= allowedTime;

            if (withinTime)
            {
                ConsecutiveSuccess++;
                if (!MinPerf.HasValue || perfRatio < MinPerf.Value.perfRatio) MinPerf = (perfRatio, op1, op2);
                if (!MaxPerf.HasValue || perfRatio > MaxPerf.Value.perfRatio) MaxPerf = (perfRatio, op1, op2);
                RemoveFromDict(_pastSlowAnswers, ordered);
            }
            else
            {
                ConsecutiveSuccess = 0;
                MinPerf = MaxPerf = null;
            }

            RemoveFromDict(_pastErrors, ordered);
            if (!withinTime) AddToDict(_pastSlowAnswers, ordered, ReinforceRepeatCap);

            message = $"Correct taking {timeUsed.TotalSeconds:F2}s, perf ratio {Answer.PerformanceRatioToReportRatio(perfRatio):F2}%\n";
            message += withinTime
                ? $"(Within time limit {allowedTime.TotalSeconds:F2}s)."
                : $"(took too long for {allowedTime.TotalSeconds:F2}s).";

            if (_pastErrors.Count > 0)
                message += $"\n({PrintRemainingPastErrorAndRepeatInstanceNumbers()})";
            else if (_pastSlowAnswers.Count > 0)
                message += $"\n({PrintRemainingPastSlowAnswers()})";
            else
                message += $"\n(ConsSucc={ConsecutiveSuccess}/{ConsecutiveSuccessesRequired})";
        }
        else
        {
            ConsecutiveSuccess = 0;
            MinPerf = MaxPerf = null;
            AddToDict(_pastErrors, ordered, ReinforceRepeatCap);
            message = $"Incorrect! ({PrintRemainingPastErrorAndRepeatInstanceNumbers()})";
        }

        var finished = _pastErrors.Count == 0 && _pastSlowAnswers.Count == 0 && ConsecutiveSuccess >= ConsecutiveSuccessesRequired;

        GenerateNextQuestion();

        return (finished, logEntry);
    }

    public void SaveLogIfEnabled()
    {
        if (string.IsNullOrEmpty(LogFilePath)) return;
        //TODO check this later..
       // System.IO.File.WriteAllLines(LogFilePath, _loggedAnswers.Select(a => a.ToString()));
    }

    // === All the helper methods from your original code (unchanged) ===
    private (int, int) GenerateOperands()
    {
        int flip = _rand.Next(0, 2);

        if (_pastErrors.Count > 0 && _rand.Next(0, 2) == 1)
        {
            var err = _pastErrors.ElementAt(_rand.Next(_pastErrors.Count));
            var o = OrderOperands(err.Key);
            return flip == 0 ? o : (o.Item2, o.Item1);
        }
        if (_pastSlowAnswers.Count > 0 && _rand.Next(0, 2) == 1)
        {
            var slow = _pastSlowAnswers.ElementAt(_rand.Next(_pastSlowAnswers.Count));
            var o = OrderOperands(slow.Key);
            return flip == 0 ? o : (o.Item2, o.Item1);
        }

        int max1 = (int)Math.Pow(10, flip == 0 ? DigitsOperand1 : DigitsOperand2);
        int max2 = (int)Math.Pow(10, flip == 0 ? DigitsOperand2 : DigitsOperand1);
        return (_rand.Next(2, max1), _rand.Next(2, max2));
    }

    private static (int, int) OrderOperands((int a, int b) t) => t.a < t.b ? t : (t.b, t.a);
    private void AddToDict(Dictionary<(int, int), int> dict, (int, int) key, int cap)
    {
        var pastMax = _pastMaxWeakness.GetValueOrDefault(key, 0);
        var newVal = dict.GetValueOrDefault(key, 0) + 1;
        if (pastMax > newVal) newVal = pastMax;
        newVal = Math.Min(newVal, cap);
        dict[key] = newVal;
        _pastMaxWeakness[key] = Math.Max(newVal, pastMax);
    }
    private static void RemoveFromDict<T>(Dictionary<T, int> dict, T key) where T : notnull
    {
        if (dict.TryGetValue(key, out var v) && v > 1) dict[key] = v - 1;
        else dict.Remove(key);
    }
    private string PrintRemainingPastErrorAndRepeatInstanceNumbers() => $"{_pastErrors.Values.Sum()} blocking question(s) remain for {_pastErrors.Count} incorrect sets.";
    private string PrintRemainingPastSlowAnswers() => $"{_pastSlowAnswers.Values.Sum()} slow answer(s) remain for {_pastSlowAnswers.Count} sets.";

    // AssessComplexity and helpers - exactly as in your original file
    public static double AssessComplexity(int a, int b, (double snc, double sc, double mnc, double mc) coeff)
    {
        if (a < b) (a, b) = (b, a);
        var d1 = ConvertNumberToDigits(a);
        var d2 = ConvertNumberToDigits(b);
        return AssessComplexity(d1, d2, coeff);
    }
    private static int[] ConvertNumberToDigits(int n)
    {
        var q = new Queue<int>();
        for (; n > 0; n /= 10) q.Enqueue(n % 10);
        return q.ToArray();
    }
    private static double AssessComplexity(int[] d1, int[] d2, (double snc, double sc, double mnc, double mc) coeff)
    {
        double total = 0;
        var (snc, sc, mnc, mc) = coeff;
        foreach (var b in d2)
        {
            for (int j = 0; j < d1.Length; j++)
            {
                var a = d1[j];
                var comp = AssessComplexitySingleDigit(a, b);
                if (j < d1.Length - 1)
                {
                    int m = a * b;
                    int carry = m / 10;
                    if (carry > 0)
                    {
                        int addend = d1[j + 1] * b;
                        bool singleDigit = addend < 10;
                        bool additionCarry = carry + (addend % 10) >= 10;
                        comp += additionCarry
                            ? (singleDigit ? sc : mc)
                            : (singleDigit ? snc : mnc);
                    }
                }
                total += comp;
            }
        }
        return total;
    }
    private static double AssessComplexitySingleDigit(int a, int b)
    {
        if (a > b) (a, b) = (b, a);
        if (a == 0) return 0.1;
        if (a == 6 && b > 6) return 1.1;
        if (a == 7 && b > 7) return 1.1;
        if (a == 5 && b == 9) return 1.0;
        return 0.9;
    }
}