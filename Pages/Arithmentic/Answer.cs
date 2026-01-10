namespace HomeHubApp.Pages.Arithmentic;

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
