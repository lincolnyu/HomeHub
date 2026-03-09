using HomeHubApp.Pages.Naplan.Models;

namespace HomeHubApp.Pages.Naplan.Services;

public interface IQuestionService
{
    List<string> GetAllTestFilePaths();

    TestConfig? LoadConfig(string fullPath);
}