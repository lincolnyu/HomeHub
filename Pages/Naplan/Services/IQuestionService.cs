using HomeHubApp.Pages.Naplan.Models;

namespace HomeHubApp.Pages.Naplan.Services;

public interface IQuestionService
{
    TestConfig GetTestConfig();
}