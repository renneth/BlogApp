using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using BlogApp.Contracts;

namespace BlogApp.Services;

// This implementation keeps the SQL logic in one file so the minimal API stays easy to follow.
public sealed class SqlBlogService : IBlogService
{
	private const string MissingSchemaMessage = "SQL blog tables were not found. Run Database/init.sql before enabling the SqlServer persistence provider.";
	private readonly string _connectionString;

	public SqlBlogService(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
		}

		_connectionString = connectionString;
		SeedIfEmpty();
	}

	public IReadOnlyList<PostSummaryDto> GetPosts()
	{
		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT p.Id,
			       p.Title,
			       p.Body,
			       p.CreatedAtUtc,
			       p.UpdatedAtUtc,
			       COUNT(c.Id) AS CommentCount
			FROM dbo.BlogPosts AS p
			LEFT JOIN dbo.BlogComments AS c ON c.PostId = p.Id
			GROUP BY p.Id, p.Title, p.Body, p.CreatedAtUtc, p.UpdatedAtUtc
			ORDER BY p.CreatedAtUtc DESC;
			""";

		using var reader = command.ExecuteReader();
		var posts = new List<PostSummaryDto>();

		while (reader.Read())
		{
			posts.Add(ReadPostSummary(reader));
		}

		return posts;
	}

	public PostDetailsDto? GetPost(int id)
	{
		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT Id,
			       Title,
			       Body,
			       CreatedAtUtc,
			       UpdatedAtUtc
			FROM dbo.BlogPosts
			WHERE Id = @Id;
			""";
		command.Parameters.Add(CreateIntParameter("@Id", id));

		using var reader = command.ExecuteReader();
		if (!reader.Read())
		{
			return null;
		}

		var post = ReadPostRow(reader);

		reader.Close();

		var comments = GetCommentsInternal(connection, id);
		return new PostDetailsDto(post.Id, post.Title, post.Body, post.CreatedAtUtc, post.UpdatedAtUtc, comments);
	}

	public PostDetailsDto CreatePost(CreatePostRequest request)
	{
		var title = CleanText(request.Title);
		var body = CleanText(request.Body);
		var createdAtUtc = DateTime.UtcNow;

		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO dbo.BlogPosts (Title, Body, CreatedAtUtc, UpdatedAtUtc)
			OUTPUT INSERTED.Id
			VALUES (@Title, @Body, @CreatedAtUtc, NULL);
			""";
		command.Parameters.Add(CreateNVarCharMaxParameter("@Title", title));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Body", body));
		command.Parameters.Add(CreateDateTime2Parameter("@CreatedAtUtc", createdAtUtc));

		var id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
		return new PostDetailsDto(id, title, body, createdAtUtc, null, []);
	}

	public PostDetailsDto? UpdatePost(int id, UpdatePostRequest request)
	{
		var title = CleanText(request.Title);
		var body = CleanText(request.Body);
		var updatedAtUtc = DateTime.UtcNow;

		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		using var command = connection.CreateCommand();
		command.CommandText = """
			UPDATE dbo.BlogPosts
			SET Title = @Title,
			    Body = @Body,
			    UpdatedAtUtc = @UpdatedAtUtc
			WHERE Id = @Id;
			""";
		command.Parameters.Add(CreateNVarCharMaxParameter("@Title", title));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Body", body));
		command.Parameters.Add(CreateDateTime2Parameter("@UpdatedAtUtc", updatedAtUtc));
		command.Parameters.Add(CreateIntParameter("@Id", id));

		return command.ExecuteNonQuery() == 0 ? null : GetPost(id);
	}

	public bool DeletePost(int id)
	{
		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM dbo.BlogPosts WHERE Id = @Id;";
		command.Parameters.Add(CreateIntParameter("@Id", id));
		return command.ExecuteNonQuery() > 0;
	}

	public IReadOnlyList<CommentDto>? GetComments(int postId)
	{
		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		if (!PostExists(connection, postId))
		{
			return null;
		}

		return GetCommentsInternal(connection, postId);
	}

	public CommentDto? AddComment(int postId, CreateCommentRequest request)
	{
		var author = CleanText(request.Author);
		var body = CleanText(request.Body);
		var createdAtUtc = DateTime.UtcNow;

		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		if (!PostExists(connection, postId))
		{
			return null;
		}

		using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO dbo.BlogComments (PostId, Author, Body, CreatedAtUtc)
			OUTPUT INSERTED.Id
			VALUES (@PostId, @Author, @Body, @CreatedAtUtc);
			""";
		command.Parameters.Add(CreateIntParameter("@PostId", postId));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Author", author));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Body", body));
		command.Parameters.Add(CreateDateTime2Parameter("@CreatedAtUtc", createdAtUtc));

		var id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
		return new CommentDto(id, author, body, createdAtUtc);
	}

	private void SeedIfEmpty()
	{
		using var connection = CreateOpenConnection();
		EnsureSchemaExists(connection);

		using var countCommand = connection.CreateCommand();
		countCommand.CommandText = "SELECT COUNT_BIG(*) FROM dbo.BlogPosts;";

		var postCount = Convert.ToInt64(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
		if (postCount > 0)
		{
			return;
		}

		// Seed the SQL database with the same starter content as the in-memory service.
		using var transaction = connection.BeginTransaction();

		var firstPostCreatedAt = DateTime.UtcNow.AddDays(-2);
		var firstCommentCreatedAt = DateTime.UtcNow.AddDays(-1);
		var secondPostCreatedAt = DateTime.UtcNow.AddHours(-12);

		var firstPostId = InsertPost(connection, transaction,
			"Welcome to the blog API",
			"This sample shows a simple in-memory CRUD API for blog posts and comments.",
			firstPostCreatedAt);

		InsertComment(connection, transaction,
			firstPostId,
			"Ren",
			"Nice starting point for learning minimal APIs.",
			firstCommentCreatedAt);

		InsertPost(connection, transaction,
			"Working with comments",
			"Comments are modeled as a child resource under each post.",
			secondPostCreatedAt);

		transaction.Commit();
	}

	private static int InsertPost(SqlConnection connection, SqlTransaction transaction, string title, string body, DateTime createdAtUtc)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO dbo.BlogPosts (Title, Body, CreatedAtUtc, UpdatedAtUtc)
			OUTPUT INSERTED.Id
			VALUES (@Title, @Body, @CreatedAtUtc, NULL);
			""";
		command.Parameters.Add(CreateNVarCharMaxParameter("@Title", title));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Body", body));
		command.Parameters.Add(CreateDateTime2Parameter("@CreatedAtUtc", createdAtUtc));
		return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	private static void InsertComment(SqlConnection connection, SqlTransaction transaction, int postId, string author, string body, DateTime createdAtUtc)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO dbo.BlogComments (PostId, Author, Body, CreatedAtUtc)
			VALUES (@PostId, @Author, @Body, @CreatedAtUtc);
			""";
		command.Parameters.Add(CreateIntParameter("@PostId", postId));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Author", author));
		command.Parameters.Add(CreateNVarCharMaxParameter("@Body", body));
		command.Parameters.Add(CreateDateTime2Parameter("@CreatedAtUtc", createdAtUtc));
		command.ExecuteNonQuery();
	}

	private static bool PostExists(SqlConnection connection, int id)
	{
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT 1 FROM dbo.BlogPosts WHERE Id = @Id;";
		command.Parameters.Add(CreateIntParameter("@Id", id));

		using var reader = command.ExecuteReader();
		return reader.Read();
	}

	private static List<CommentDto> GetCommentsInternal(SqlConnection connection, int postId)
	{
		using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT Id,
			       Author,
			       Body,
			       CreatedAtUtc
			FROM dbo.BlogComments
			WHERE PostId = @PostId
			ORDER BY CreatedAtUtc ASC, Id ASC;
			""";
		command.Parameters.Add(CreateIntParameter("@PostId", postId));

		using var reader = command.ExecuteReader();
		var comments = new List<CommentDto>();

		while (reader.Read())
		{
			comments.Add(ReadComment(reader));
		}

		return comments;
	}

	private static void EnsureSchemaExists(SqlConnection connection)
	{
		using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT CASE
			           WHEN OBJECT_ID(N'dbo.BlogPosts', N'U') IS NOT NULL
			            AND OBJECT_ID(N'dbo.BlogComments', N'U') IS NOT NULL
			           THEN 1
			           ELSE 0
			       END;
			""";

		var schemaExists = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
		if (!schemaExists)
		{
			throw new InvalidOperationException(MissingSchemaMessage);
		}
	}

	private SqlConnection CreateOpenConnection()
	{
		// Open a short-lived connection for each service call.
		var connection = new SqlConnection(_connectionString);
		connection.Open();
		return connection;
	}

	private static PostSummaryDto ReadPostSummary(SqlDataReader reader)
	{
		var body = reader.GetString(2);
		return new PostSummaryDto(
			reader.GetInt32(0),
			reader.GetString(1),
			CreateExcerpt(body),
			reader.GetInt32(5),
			reader.GetDateTime(3),
			reader.IsDBNull(4) ? null : reader.GetDateTime(4));
	}

	private static PostRow ReadPostRow(SqlDataReader reader)
	{
		return new PostRow(
			reader.GetInt32(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetDateTime(3),
			reader.IsDBNull(4) ? null : reader.GetDateTime(4));
	}

	private static CommentDto ReadComment(SqlDataReader reader)
	{
		return new CommentDto(
			reader.GetInt32(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetDateTime(3));
	}

	private static SqlParameter CreateIntParameter(string name, int value)
	{
		return new SqlParameter(name, SqlDbType.Int) { Value = value };
	}

	private static SqlParameter CreateDateTime2Parameter(string name, DateTime value)
	{
		return new SqlParameter(name, SqlDbType.DateTime2) { Value = value };
	}

	private static SqlParameter CreateNVarCharMaxParameter(string name, string value)
	{
		return new SqlParameter(name, SqlDbType.NVarChar, -1) { Value = value };
	}

	private static string CleanText(string? value)
	{
		return value?.Trim() ?? string.Empty;
	}

	private static string CreateExcerpt(string value)
	{
		return value.Length <= 100 ? value : value[..100] + "...";
	}

	private sealed record PostRow(int Id, string Title, string Body, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
}