namespace HomeHubApp.Pages.Naplan.Models
{
    public class TestConfig
    {
        public int? TotalTimeSeconds { get; set; }
        public List<Question> Questions { get; set; } = new();
    }
}