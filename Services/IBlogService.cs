using BlogApp.Contracts;

namespace BlogApp.Services;

// The API talks to this interface so the storage choice can change without touching the endpoints.
public interface IBlogService
{
	IReadOnlyList<PostSummaryDto> GetPosts();

	PostDetailsDto? GetPost(int id);

	PostDetailsDto CreatePost(CreatePostRequest request);

	PostDetailsDto? UpdatePost(int id, UpdatePostRequest request);

	bool DeletePost(int id);

	IReadOnlyList<CommentDto>? GetComments(int postId);

	CommentDto? AddComment(int postId, CreateCommentRequest request);
}