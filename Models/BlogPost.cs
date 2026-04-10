namespace BlogApp.Models;

public sealed class BlogPost
{
	public int Id { get; init; }

	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; init; }

	public DateTime? UpdatedAtUtc { get; set; }

	public List<BlogComment> Comments { get; } = [];
}