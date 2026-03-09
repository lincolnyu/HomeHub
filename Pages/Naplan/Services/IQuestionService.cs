using HomeHubApp.Pages.Naplan.Models;

namespace HomeHubApp.Pages.Naplan.Services;

public interface IQuestionService
{
    IWebHostEnvironment Environment { get; }

    List<string> GetAllTestFilePaths();

    TestConfig? LoadConfig(string fullPath);
}