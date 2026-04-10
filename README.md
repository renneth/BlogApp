# BlogApp

This project is a small ASP.NET Core minimal API sample for blog posts and comments. A single entry point, small DTOs, one service interface, and two storage implementations.

## Notes

- The API uses the in-memory service by default.
- The same API can also run against SQL Server by switching one configuration value. Instruction below.
- The browser UI in `wwwroot` exists only to provide demonstration of API from a simple page.
- Sample seed data is included in both service implementations so the app has content on first run.

## Features

- List blog posts
- View a single post with comments
- Create, update, and delete posts
- Add comments to a post
- Switch between `InMemory` storage and `SqlServer` storage in appsettings

## Project layout

- `Program.cs`: application startup, configuration, validation helpers, and route mapping
- `Contracts/BlogDtos.cs`: request and response records used by the API
- `Models/`: simple in-memory model classes
- `Services/IBlogService.cs`: shared contract for both storage implementations
- `Services/InMemoryBlogService.cs`: easiest implementation to read and run
- `Services/SqlBlogService.cs`: SQL Server implementation using `Microsoft.Data.SqlClient`
- `Database/init.sql`: creates the SQL database and tables
- `wwwroot/`: small HTML, CSS, and JavaScript client for manual testing
- `BlogApp.http`: ready-made HTTP requests for quick endpoint checks

## API summary

- `GET /posts`: list posts
- `GET /posts/{id}`: get one post with comments
- `POST /posts`: create a post
- `PUT /posts/{id}`: update a post
- `DELETE /posts/{id}`: delete a post
- `GET /posts/{id}/comments`: list comments for a post
- `POST /posts/{id}/comments`: add a comment

## How to run

### Default path: in-memory storage

1. Run `dotnet run`.
2. Open `http://localhost:5030` for the demo UI or use `BlogApp.http` for direct API calls.

No database setup is required for this path.

### Optional path: SQL Server storage

1. Run `Database/init.sql` against SQL Server.
2. Set `Persistence:Provider` to `SqlServer`.
3. Update `ConnectionStrings:BlogDatabase` with a real connection string.
4. Run `dotnet run`.