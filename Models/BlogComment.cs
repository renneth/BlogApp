namespace BlogApp.Models;

public sealed class BlogComment
{
	public int Id { get; init; }

	public string Author { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; init; }
}