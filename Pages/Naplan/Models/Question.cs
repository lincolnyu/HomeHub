using System.ComponentModel.DataAnnotations;

namespace HomeHubApp.Pages.Naplan.Models;

public class Question
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? Type { get; set; } = "single"; // single, multi, text, none

    public int? ParentId { get; set; }               // ← NEW: links to parent informational page

    public List<string> Options { get; set; } = new();

    public List<string> CorrectAnswers { get; set; } = new();
}