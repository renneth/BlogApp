-- Create the sample database if it does not exist yet.
IF DB_ID(N'BlogApp') IS NULL
BEGIN
	CREATE DATABASE [BlogApp];
END;
GO

-- Switch to the sample database before creating tables.
USE [BlogApp];
GO

-- Store the main blog posts here.
IF OBJECT_ID(N'dbo.BlogPosts', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.BlogPosts
	(
		Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BlogPosts PRIMARY KEY,
		Title NVARCHAR(MAX) NOT NULL,
		Body NVARCHAR(MAX) NOT NULL,
		CreatedAtUtc DATETIME2 NOT NULL,
		UpdatedAtUtc DATETIME2 NULL
	);
END;
GO

-- Store comments in a child table linked to each post.
IF OBJECT_ID(N'dbo.BlogComments', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.BlogComments
	(
		Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BlogComments PRIMARY KEY,
		PostId INT NOT NULL,
		Author NVARCHAR(MAX) NOT NULL,
		Body NVARCHAR(MAX) NOT NULL,
		CreatedAtUtc DATETIME2 NOT NULL,
		CONSTRAINT FK_BlogComments_BlogPosts FOREIGN KEY (PostId)
			REFERENCES dbo.BlogPosts (Id)
			ON DELETE CASCADE
	);

	CREATE INDEX IX_BlogComments_PostId ON dbo.BlogComments (PostId);
END;
GO