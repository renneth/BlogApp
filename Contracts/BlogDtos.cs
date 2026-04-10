namespace BlogApp.Contracts;

// Request records represent the JSON body sent by the client.
public record CreatePostRequest(string? Title, string? Body);

public record UpdatePostRequest(string? Title, string? Body);

public record CreateCommentRequest(string? Author, string? Body);

// Response records keep API output separate from the internal model classes.
public record PostSummaryDto(
	int Id,
	string Title,
	string Excerpt,
	int CommentCount,
	DateTime CreatedAtUtc,
	DateTime? UpdatedAtUtc);

public record CommentDto(
	int Id,
	string Author,
	string Body,
	DateTime CreatedAtUtc);

public record PostDetailsDto(
	int Id,
	string Title,
	string Body,
	DateTime CreatedAtUtc,
	DateTime? UpdatedAtUtc,
	IReadOnlyList<CommentDto> Comments);