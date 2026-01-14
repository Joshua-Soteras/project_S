# Backend File Structure

## Core Files

**[Program.cs](Program.cs)** - Main application entry point
- Configures the web application and dependency injection container
- Sets up the HTTP request pipeline
- Contains minimal API endpoints (currently has a sample `/weatherforecast` endpoint)
- Uses top-level statements (no `Main` method needed in .NET 10)

**[backend.csproj](backend.csproj)** - Project file
- Defines project metadata and dependencies
- Target framework: .NET 10.0
- Includes `Microsoft.AspNetCore.OpenApi` package for API documentation
- Enables nullable reference types and implicit usings

## Configuration Files

**[appsettings.json](appsettings.json)** - Production configuration
- Logging levels (default: Information, ASP.NET Core: Warning)
- AllowedHosts: "*" (accepts requests from any host)
- Used in all environments

**[appsettings.Development.json](appsettings.Development.json)** - Development-specific config
- Overrides `appsettings.json` when running in Development mode
- Currently just contains logging configuration

## Properties Directory

**[Properties/launchSettings.json](Properties/launchSettings.json)** - Launch profiles
- Defines how the app runs during development
- **http profile**: Runs on `http://localhost:5066`
- **https profile**: Runs on `https://localhost:7077` and `http://localhost:5066`
- Sets `ASPNETCORE_ENVIRONMENT=Development`

## Testing File

**[backend.http](backend.http)** - HTTP request file
- Contains sample HTTP requests for testing endpoints
- Can be used with VS Code REST Client or Visual Studio
- Currently has a GET request for the weather forecast endpoint

## Build Output Directories

**bin/** - Compiled binaries
- Created during build process
- Contains Debug/Release folders with compiled DLLs

**obj/** - Intermediate build files
- Temporary files used during compilation
- Should be in `.gitignore`

## Current API Structure

The template includes one sample endpoint:
- **GET /weatherforecast** - Returns 5 days of random weather data
- Uses minimal API style (no controllers)
- Returns JSON by default
- Includes OpenAPI documentation support
