using BlogApp.Contracts;
using BlogApp.Models;

namespace BlogApp.Services;

public sealed class InMemoryBlogService : IBlogService
{
	// ASP.NET can serve multiple requests at the same time, so protect the shared in-memory data.
	private readonly object _gate = new();
	private readonly Dictionary<int, BlogPost> _posts = new();
	private int _nextPostId = 1;
	private int _nextCommentId = 1;

	public InMemoryBlogService()
	{
		AddSampleData();
	}

	public IReadOnlyList<PostSummaryDto> GetPosts()
	{
		lock (_gate)
		{
			return _posts.Values
					.OrderByDescending(post => post.CreatedAtUtc)
					.Select(MapSummary)
					.ToList();
		}
	}

	public PostDetailsDto? GetPost(int id)
	{
		lock (_gate)
		{
			return _posts.TryGetValue(id, out var post)
					? MapDetails(post)
					: null;
		}
	}

	public PostDetailsDto CreatePost(CreatePostRequest request)
	{
		lock (_gate)
		{
			var post = new BlogPost
			{
				Id = _nextPostId++,
				Title = CleanText(request.Title),
				Body = CleanText(request.Body),
				CreatedAtUtc = DateTime.UtcNow
			};

			_posts[post.Id] = post;
			return MapDetails(post);
		}
	}

	public PostDetailsDto? UpdatePost(int id, UpdatePostRequest request)
	{
		lock (_gate)
		{
			if (!_posts.TryGetValue(id, out var post))
			{
				return null;
			}

			post.Title = CleanText(request.Title);
			post.Body = CleanText(request.Body);
			post.UpdatedAtUtc = DateTime.UtcNow;

			return MapDetails(post);
		}
	}

	public bool DeletePost(int id)
	{
		lock (_gate)
		{
			return _posts.Remove(id);
		}
	}

	public IReadOnlyList<CommentDto>? GetComments(int postId)
	{
		lock (_gate)
		{
			return _posts.TryGetValue(postId, out var post)
					? post.Comments.Select(MapComment).ToList()
					: null;
		}
	}

	public CommentDto? AddComment(int postId, CreateCommentRequest request)
	{
		lock (_gate)
		{
			if (!_posts.TryGetValue(postId, out var post))
			{
				return null;
			}

			var comment = new BlogComment
			{
				Id = _nextCommentId++,
				Author = CleanText(request.Author),
				Body = CleanText(request.Body),
				CreatedAtUtc = DateTime.UtcNow
			};

			post.Comments.Add(comment);
			return MapComment(comment);
		}
	}

	private void AddSampleData()
	{
		// Seed a couple of posts so the app has something to show on first run.
		var firstPost = new BlogPost
		{
			Id = _nextPostId++,
			Title = "Welcome to the blog API",
			Body = "This sample shows a simple in-memory CRUD API for blog posts and comments.",
			CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
		};

		firstPost.Comments.Add(new BlogComment
		{
			Id = _nextCommentId++,
			Author = "Ren",
			Body = "Nice starting point for learning minimal APIs.",
			CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
		});

		var secondPost = new BlogPost
		{
			Id = _nextPostId++,
			Title = "Working with comments",
			Body = "Comments are modeled as a child resource under each post.",
			CreatedAtUtc = DateTime.UtcNow.AddHours(-12)
		};

		_posts[firstPost.Id] = firstPost;
		_posts[secondPost.Id] = secondPost;
	}

	private static PostSummaryDto MapSummary(BlogPost post)
	{
		var excerpt = CreateExcerpt(post.Body);

		return new PostSummaryDto(
			post.Id,
			post.Title,
			excerpt,
			post.Comments.Count,
			post.CreatedAtUtc,
			post.UpdatedAtUtc);
	}

	private static PostDetailsDto MapDetails(BlogPost post)
	{
		return new PostDetailsDto(
			post.Id,
			post.Title,
			post.Body,
			post.CreatedAtUtc,
			post.UpdatedAtUtc,
			post.Comments.Select(MapComment).ToList());
	}

	private static CommentDto MapComment(BlogComment comment)
	{
		return new CommentDto(comment.Id, comment.Author, comment.Body, comment.CreatedAtUtc);
	}

	private static string CleanText(string? value)
	{
		return value?.Trim() ?? string.Empty;
	}

	private static string CreateExcerpt(string value)
	{
		return value.Length <= 100 ? value : value[..100] + "...";
	}
}