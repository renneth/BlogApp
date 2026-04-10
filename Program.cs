using BlogApp.Contracts;
using BlogApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Expose OpenAPI in development so reviewers can inspect the endpoints quickly.
builder.Services.AddOpenApi();

// Keep the storage choice simple: use the in-memory service by default,
// or switch to SQL Server through configuration.
builder.Services.AddSingleton<IBlogService>(CreateBlogService(builder.Configuration));

var configuredUrls = builder.Configuration[WebHostDefaults.ServerUrlsKey];
var hasHttpsEndpoint = configuredUrls?
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    ?? false;

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Only redirect when this run profile actually exposes HTTPS.
if (hasHttpsEndpoint)
{
    app.UseHttpsRedirection();
}

// Serve the small demo UI from wwwroot/index.html.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/app-info", (IConfiguration configuration) => Results.Ok(new
{
    storageProvider = GetStorageProviderDisplayName(configuration)
}))
    .WithName("GetAppInfo")
    .WithSummary("Get metadata for the demo UI");

// Group all blog post endpoints under /posts.
var posts = app.MapGroup("/posts").WithTags("Posts");

posts.MapGet("", (IBlogService blogService) => Results.Ok(blogService.GetPosts()))
    .WithName("GetPosts")
    .WithSummary("Get all blog posts");

posts.MapGet("/{id:int}", (int id, IBlogService blogService) =>
{
    var post = blogService.GetPost(id);
    return post is null
        ? Results.NotFound(new { message = $"Post {id} was not found." })
        : Results.Ok(post);
})
    .WithName("GetPostById")
    .WithSummary("Get a blog post by id");

posts.MapPost("", (CreatePostRequest request, IBlogService blogService) =>
{
    var validationError = ValidatePostRequest(request.Title, request.Body);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var createdPost = blogService.CreatePost(request);
    return Results.Created($"/posts/{createdPost.Id}", createdPost);
})
    .WithName("CreatePost")
    .WithSummary("Create a new blog post");

posts.MapPut("/{id:int}", (int id, UpdatePostRequest request, IBlogService blogService) =>
{
    var validationError = ValidatePostRequest(request.Title, request.Body);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var updatedPost = blogService.UpdatePost(id, request);
    return updatedPost is null
        ? Results.NotFound(new { message = $"Post {id} was not found." })
        : Results.Ok(updatedPost);
})
    .WithName("UpdatePost")
    .WithSummary("Replace an existing blog post");

posts.MapDelete("/{id:int}", (int id, IBlogService blogService) =>
{
    var deleted = blogService.DeletePost(id);
    return deleted
        ? Results.NoContent()
        : Results.NotFound(new { message = $"Post {id} was not found." });
})
    .WithName("DeletePost")
    .WithSummary("Delete a blog post");

posts.MapGet("/{id:int}/comments", (int id, IBlogService blogService) =>
{
    var comments = blogService.GetComments(id);
    return comments is null
        ? Results.NotFound(new { message = $"Post {id} was not found." })
        : Results.Ok(comments);
})
    .WithName("GetPostComments")
    .WithSummary("Get comments for a post");

posts.MapPost("/{id:int}/comments", (int id, CreateCommentRequest request, IBlogService blogService) =>
{
    var validationError = ValidateCommentRequest(request.Author, request.Body);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var comment = blogService.AddComment(id, request);
    return comment is null
        ? Results.NotFound(new { message = $"Post {id} was not found." })
        : Results.Created($"/posts/{id}/comments/{comment.Id}", comment);
})
    .WithName("AddPostComment")
    .WithSummary("Add a comment to a post");

app.Run();

static IBlogService CreateBlogService(ConfigurationManager configuration)
{
    var provider = configuration["Persistence:Provider"];

    if (string.IsNullOrWhiteSpace(provider)
        || string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        return new InMemoryBlogService();
    }

    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = configuration.GetConnectionString("BlogDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'BlogDatabase' is required when Persistence:Provider is set to SqlServer.");
        }

        return new SqlBlogService(connectionString);
    }

    throw new InvalidOperationException($"Unsupported persistence provider '{provider}'. Supported values are InMemory and SqlServer.");
}

static string GetStorageProviderDisplayName(IConfiguration configuration)
{
    var provider = configuration["Persistence:Provider"];

    if (string.IsNullOrWhiteSpace(provider)
        || string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        return "In-memory storage";
    }

    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        return "SQL Server";
    }

    return provider;
}

static string? ValidatePostRequest(string? title, string? body)
{
    if (string.IsNullOrWhiteSpace(title))
    {
        return "Post title is required.";
    }

    if (string.IsNullOrWhiteSpace(body))
    {
        return "Post body is required.";
    }

    return null;
}

static string? ValidateCommentRequest(string? author, string? body)
{
    if (string.IsNullOrWhiteSpace(author))
    {
        return "Comment author is required.";
    }

    if (string.IsNullOrWhiteSpace(body))
    {
        return "Comment body is required.";
    }

    return null;
}
