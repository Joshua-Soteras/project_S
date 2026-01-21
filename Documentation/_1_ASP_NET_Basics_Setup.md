Joshua Soteras
January 21, 2026

--- 
## Sources

- ASP.NET Core Documentations + Tutorial 
	- https://learn.microsoft.com/en-us/ef/core/
- Db Context 
	- https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
	- Units of Work
		- https://www.martinfowler.com/eaaCatalog/unitOfWork.html 
	- 
- 
- https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-10.0#secure-authentication-flows

---

## Installation 
*With the regard to use Visual Studio Code (Blue Icon)*
### 1. Install .NET SDK

Download and install the .NET SDK (version 8.0 or later recommended):

**macOS (using Homebrew):**

```bash
brew install dotnet-sdk
```

**macOS/Windows/Linux (manual download):**

- Visit: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- Download the SDK (not just the runtime)
- Run the installer

**Verify installation:**

```bash
dotnet --version
```

You should see something like `8.0.xxx`.

### 2. Install EF Core CLI Tools (Global)

Entity Framework Core command-line tools are required for migrations:

```bash
dotnet tool install --global dotnet-ef
```

**Verify installation:**

```bash
dotnet ef --version
```

**If already installed, update to latest:**

```bash
dotnet tool update --global dotnet-ef
```

### 3. Set up Entity Framework (EF) Core + Postgres SQL Packages 

*Be sure to be in the correct directory*

```
cd backend/src/_02_Infrastructure

# Add PostgreSQL provider
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

cd ../_03_Web_API

# Add EF Core design tools (for migrations)
dotnet add package Microsoft.EntityFrameworkCore.Design


```

--- 

## Files and Purpose
### **Root Configuration Files**


### Core Files

| **File/Folder**  | **Full Detail & Responsibility**                                                                                                                                                                                                | **Why it's Important**                                                                                             |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| **`Program.cs`** | This is the **Entry Point**. It contains the `Main` method where the application starts. It sets up the `WebApplicationBuilder`, registers services (Dependency Injection), and defines the HTTP request pipeline (Middleware). | Without this, the application cannot start. It is the "brain" that connects your code to the web server (Kestrel). |
| **`bin/`**       | Short for **Binary**. This folder holds the actual executable files. After a successful build, your C# code is converted into Intermediate Language (IL) stored in `.dll` files here.                                           | This folder contains what you actually "publish" or deploy to a server to make the site go live.                   |
| **`obj/`**       | Short for **Object**. It contains temporary intermediate files created during the compilation process. It also stores the `project.assets.json`file created by `dotnet restore`.                                                | It speeds up compilation by allowing the builder to perform "incremental builds" (only compiling what changed).    |

---
##  Flow 
This still needs 
Create Entity -> Register in DbContext -> Connection String -> Program.cs (wires everything together via DI  )

--- 
## C# Basics 

**using *namespace*** 
- Just a way for the code to understand how to group your files together
- If the project folder is project_s I could use the name space like "sadfsad.sadfsad.here" right. as long as I am consistent with using this as a group


--- 
## Entities 
**Definiton** 
- Creating C# class that maps to a database table (SQL )
- Each instance/object becomes a row within the table 
- See conventions sections for how each property in the class becomes an attribute in a table

**Two Ways of Creating Entities** 
1. Data Annotations
2. Fluent API 

**Conventions** 
EF Core uses **conventions** (naming patterns) to figure out your schema:

|Convention|Example|Result|
|---|---|---|
|Property named `Id`|`public int Id`|Primary key, auto-increment|
|Property named `{Class}Id`|`public int UserId`|Also detected as primary key|
|Non-nullable type|`string Email`|NOT NULL column|
|Nullable type|`string? DisplayName`|NULL allowed|
|Navigation property|`public List<Post> Posts`|One-to-many relationship|
|Foreign key|`public int AuthorId`|FK if `Author` navigation exists|

--- 
## Entities: Data Annotations vs Fluent API

- **Data Annotations** 
	- sticky notes you put directly _on_ the item (the class properties). It's quick and visible immediately.
    
- **Fluent API** 
	- separate master blueprint or rulebook kept in the manager's office (the `DbContext`). 
	- It keeps the items clean but requires you to look elsewhere for the rules.


### Example 1: Basic Validation (Required & Length) 

#### Example 1: Data Annotations
```
public class User
{
    public int Id { get; set; }

    [Required]              // Cannot be null
    [MaxLength(50)]         // Max 50 chars
    [Column("user_name")]   // Map to specific column name
    public string Username { get; set; }
}
```

#### Example 1: Fluent API 

```

public class User
{
    public int Id { get; set; }

    [Required]              // Cannot be null
    [MaxLength(50)]         // Max 50 chars
    [Column("user_name")]   // Map to specific column name
    public string Username { get; set; }
}
```

### Example 2: Relationships (Foreign Keys)

#### Example 2:  Data Annotations
```
public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; }

    // This is the actual data column
    public int AuthorId { get; set; }

    // This is the navigation property (the object)
    [ForeignKey("AuthorId")] 
    public Author Author { get; set; }
}
```

#### Example 2: Fluent API 

```
public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; }

    // This is the actual data column
    public int AuthorId { get; set; }

    // This is the navigation property (the object)
    [ForeignKey("AuthorId")] 
    public Author Author { get; set; }
}
```


### Example 3: Composite Keys 

This is a scenario where **Data Annotations fail**. 
- If you need a primary key made of _two_ columns combined (like in a join table for a generic many-to-many relationship), 
	-  **must** use Fluent API in EF Core (prior to .NET 7, though Fluent API is still preferred for this).


---
## DbContext

**Definiton** 
- Middleman / Translator between C# code and the Database
- Database speaks in SQL and the application speaks in C#
	- Two different languages; how can they understand each other? 
	- So DbContext solves this 
- A *Special class* that provides Entity Framework Core (EF core)
### Three Main Components
1. Gateway
2. Translator 
	- C# Class -> Database Table
	- C# Property -> Database Column 
3. Tracker
	- This is its "superpower." 
	- When you pull data out of the database, the `DbContext` remembers what that data looked like. If you change a user's name in your code, the `DbContext` notices the change. 
	- When you say "Save," it automatically writes the exact SQL needed to update just that one name.

--- 
