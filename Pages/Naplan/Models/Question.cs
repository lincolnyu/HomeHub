using System.ComponentModel.DataAnnotations;

namespace HomeHubApp.Pages.Naplan.Models;

public class Question
{
    public int Id { get; set; }

    [Required] public string Title { get; set; } = string.Empty;

    [Required] public string Content { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public List<string> Options { get; set; } = new();

    [Required] public string CorrectAnswer { get; set; } = string.Empty;
}