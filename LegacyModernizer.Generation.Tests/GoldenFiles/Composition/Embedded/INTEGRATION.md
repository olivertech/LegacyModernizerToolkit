# Integration Guide

This package was generated in `Embedded` mode to be copied into an existing solution and used as a client module for an existing API.

The goal of this guide is to explain, step by step, how to place the projects correctly, how to reference them, how to register them in dependency injection, and how to validate the final integration.

## Quick Integration Checklist

Use this checklist when integrating the generated module into the host solution:

1. Copy the 3 generated folders into the host solution `src` folder.
2. Keep the 3 generated projects as siblings under the same parent folder.
3. Add the 3 `.csproj` files to the host `.sln`.
4. Run `dotnet restore`.
5. Add a project reference from the host application to `AlphaSquad.Lmt.Application.Http`.
6. Configure the API base URL using `Apis:AlphaSquad:BaseUrl`.
7. If the module was generated with `AccessTokenAccessor`, create and register `IAccessTokenAccessor`.
8. Register `AddGeneratedApi(baseUrl)` in `Program.cs` or `Startup.cs`.
9. Run `dotnet build`.
10. Consume only the generated contracts and services.

## Generated Projects

- `AlphaSquad.Lmt.Application.Contracts`
- `AlphaSquad.Lmt.Application.ApiClient`
- `AlphaSquad.Lmt.Application.Http`

These projects are usually located inside the generated package under:

```text
src/
  AlphaSquad.Lmt.Application.Contracts
  AlphaSquad.Lmt.Application.ApiClient
  AlphaSquad.Lmt.Application.Http
```

## Before You Start

Before adding these projects into your host solution, check the points below:

1. The three generated projects must stay together under the same parent folder.
2. Do not rename the folders or `.csproj` files before the first successful build.
3. Do not add only one or two projects. The three projects are required together.
4. The consuming application should reference only `AlphaSquad.Lmt.Application.Http`.
5. `AlphaSquad.Lmt.Application.ApiClient` and `AlphaSquad.Lmt.Application.Contracts` are consumed transitively by `AlphaSquad.Lmt.Application.Http`.

## Purpose Of Each Project

### `AlphaSquad.Lmt.Application.Contracts`

Contains the public contracts that the host application should reference directly:

- DTOs
- `IApiFacade`
- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`
- `IAuthenticationService` / `AuthenticationService`

### `AlphaSquad.Lmt.Application.ApiClient`

Contains the raw `Kiota` client generated from the OpenAPI specification.

This project exists as a technical dependency and should not be consumed directly by pages, controllers, view models or application use cases.

### `AlphaSquad.Lmt.Application.Http`

Contains the implementation layer:

- facades
- services
- mappers
- dependency injection bootstrap
- generated authentication support

## Recommended Folder Layout Inside The Host Solution

The easiest and safest integration is to place the generated projects as siblings of the existing host projects.

Example:

```text
src/
  AlphaSquad.Web
  AlphaSquad.Lmt.Application.Contracts
  AlphaSquad.Lmt.Application.ApiClient
  AlphaSquad.Lmt.Application.Http
```

If you place the generated projects in a different folder structure, the relative `ProjectReference` paths inside the generated `.csproj` files may no longer be valid. In that case you must manually adjust the `ProjectReference Include="..."` entries.

## Step 1 - Copy The Generated Projects To The Host Solution

Copy the following folders from the generated package into the `src` folder of the host solution:

- `src/AlphaSquad.Lmt.Application.Contracts`
- `src/AlphaSquad.Lmt.Application.ApiClient`
- `src/AlphaSquad.Lmt.Application.Http`

Expected result:

```text
YourHostSolution/
  src/
    AlphaSquad.Web/
    AlphaSquad.Lmt.Application.Contracts/
    AlphaSquad.Lmt.Application.ApiClient/
    AlphaSquad.Lmt.Application.Http/
```

Only after the folders are copied to their final place should you add them to the `.sln`.

## Step 2 - Add The Generated Projects To The Solution

The generated projects already include explicit package versions and opt out of central package version management in `Embedded` mode.
This is intentional, so the imported module can compile more predictably even when the host solution has its own package policy.

Open a terminal in the root folder of the host solution and run:

```powershell
dotnet sln add .\src\AlphaSquad.Lmt.Application.Contracts\AlphaSquad.Lmt.Application.Contracts.csproj
dotnet sln add .\src\AlphaSquad.Lmt.Application.ApiClient\AlphaSquad.Lmt.Application.ApiClient.csproj
dotnet sln add .\src\AlphaSquad.Lmt.Application.Http\AlphaSquad.Lmt.Application.Http.csproj
```

If you prefer, you can also add them through Visual Studio:

1. Right click the solution.
2. Choose `Add > Existing Project`.
3. Select the three generated `.csproj` files.

### Important note about packages

When these projects are copied into another solution, the package references should remain explicit inside the generated `.csproj` files.
If your host solution enforces a custom package policy, review these versions before changing them:

- `Microsoft.Kiota.*`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Http`

If the host solution uses `Directory.Packages.props` or another central package strategy, do not change the generated projects before the first successful restore and build.

## Step 3 - Restore Packages

After adding the projects to the solution, run:

```powershell
dotnet restore
```

This step helps surface package conflicts before the first full build.

## Step 4 - Reference The HTTP Project From The Consuming Application

The host application usually needs to reference only the HTTP project directly, because it already depends on `Contracts` and `ApiClient`.

Typical consuming applications:

- MVC / Razor project
- Admin dashboard
- Web API that orchestrates another API
- MAUI host project
- Blazor host project

If the host project is `src/AlphaSquad.Web\AlphaSquad.Web.csproj`, run:

```powershell
dotnet add .\src\AlphaSquad.Web\AlphaSquad.Web.csproj reference .\src\AlphaSquad.Lmt.Application.Http\AlphaSquad.Lmt.Application.Http.csproj
```

Important:

- add the reference to `AlphaSquad.Lmt.Application.Http`
- do not add a direct reference from the host project to `AlphaSquad.Lmt.Application.ApiClient`
- do not inject Kiota builders directly into controllers, pages or use cases

## Step 5 - Configure The API Base URL

Add the API base URL to the host application's configuration.

Suggested API base URL captured during generation:

```text
https://api.example.com
```

This value was inferred from the specification source informed in the LMT.
If the specification URL was, for example, a Swagger or OpenAPI endpoint such as:

- `https://localhost:7054/swagger/v1/swagger.json`
- `https://api.company.com/swagger/v1/swagger.json`
- `https://api.company.com/my-app/openapi/v1.json`

the guide tries to suggest the base API URL that the host application should call.
Always review this value before finishing the integration, especially when the API is hosted behind a gateway, reverse proxy or virtual directory.

Example file:

- `src\AlphaSquad.Web\appsettings.json`

Example section:

```json
{
  "Apis": {
    "AlphaSquad": {
      "BaseUrl": "https://api.example.com"
    }
  }
}
```

Configuration key used in the sample below:

- `Apis:AlphaSquad:BaseUrl`

## Step 6 - Register The Generated Module In Dependency Injection

In the startup file of the host application, register the generated module using:

- `AlphaSquad.Lmt.Application.Http.DependencyInjection.ServiceCollectionExtensions`
- method: `AddGeneratedApi(baseUrl)`

Typical files:

- `Program.cs`
- `Startup.cs`

Minimal registration example:

```csharp
using AlphaSquad.Lmt.Application.Http.DependencyInjection;

builder.Services.AddGeneratedApi("https://api.example.com");
```

If your API base URL comes from configuration, prefer something like:

```csharp
using AlphaSquad.Lmt.Application.Http.DependencyInjection;

var apiBaseUrl = builder.Configuration["Apis:AlphaSquad:BaseUrl"]
                 ?? throw new InvalidOperationException("API base URL was not configured.");

builder.Services.AddGeneratedApi(apiBaseUrl);
```

## Authentication Setup

This output was generated with `AccessTokenAccessor` mode. That means the host application must provide an implementation of:

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`

Create this implementation inside the consuming application project, not inside the generated projects.
Good locations:

- `src/AlphaSquad.Admin/Security/AccessTokenAccessor.cs`
- `src\AlphaSquad.Web\Security\AccessTokenAccessor.cs`
- `src/AlphaSquad.App/Authentication/AccessTokenAccessor.cs`

Example file:

- `src\AlphaSquad.Web\Security\AccessTokenAccessor.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AlphaSquad.Lmt.Application.Contracts.Interfaces;

namespace AlphaSquad.Web.Security;

public sealed class AccessTokenAccessor(IHttpContextAccessor httpContextAccessor) : IAccessTokenAccessor
{
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("access_token");
        return Task.FromResult(token);
    }
}
```

If your application stores the token in another place, adapt only this class. The generated module does not need to change.

## Step 7 - Full Program.cs Example

The example below shows a complete `Program.cs` for an ASP.NET Core MVC or Razor application using:

- session to store the access token
- `IAccessTokenAccessor`
- `AddGeneratedApi(baseUrl)`
- MVC controllers and views

Example file:

- `src\AlphaSquad.Web\Program.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AlphaSquad.Lmt.Application.Contracts.Interfaces;
using AlphaSquad.Lmt.Application.Http.DependencyInjection;
using AlphaSquad.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

var apiBaseUrl = builder.Configuration["Apis:AlphaSquad:BaseUrl"]
                 ?? throw new InvalidOperationException("The API base URL was not configured.");

builder.Services.AddScoped<IAccessTokenAccessor, AccessTokenAccessor>();
builder.Services.AddGeneratedApi(apiBaseUrl);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

If your team prefers to keep `AccessTokenAccessor` in its own file, keep the class in `Security/AccessTokenAccessor.cs` and leave `Program.cs` only with the DI registration line:

```csharp
builder.Services.AddScoped<IAccessTokenAccessor, AccessTokenAccessor>();
```

## Step 8 - Consume Only The Generated Contracts And Services

After registration, the consuming application should request dependencies from DI using the generated interfaces.

Prefer using:

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IApiFacade`
- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`
- `IAuthenticationService` / `AuthenticationService`

Do not consume:

- raw Kiota request builders
- Kiota models directly in pages or controllers
- `AlphaSquad.Lmt.Application.ApiClient` as a public API surface

## Step 9 - Example Of Consumption In The Host Application

### Example in a controller or page model

```csharp
using Microsoft.AspNetCore.Mvc;
using AlphaSquad.Lmt.Application.Contracts.Interfaces;

public sealed class DashboardController : Controller
{
    private readonly IAuthenticationService _authenticationService;

    public DashboardController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _authenticationService.GETAuthenticationMeAsync(cancellationToken);
        return View(result);
    }
}
```

### Important rule

The host application should depend on service and facade contracts, not on Kiota internals.

## Step 10 - Validate The Integration

Run the commands below in the root of the host solution:

```powershell
dotnet restore
dotnet build
```

If the build succeeds, the module is structurally integrated.

If the host application already has tests, also run:

```powershell
dotnet test
```

If you want a quick smoke test after build:

```powershell
dotnet run --project .\src\AlphaSquad.Web\AlphaSquad.Web.csproj
```

## Main Contracts

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IApiFacade`
- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`
- `IAuthenticationService` / `AuthenticationService`

## Troubleshooting

If the integration does not work as expected, verify these points first:

1. The three generated folders were copied as siblings under the same parent folder.
2. The three generated projects were added to the target solution.
3. The consuming application references `AlphaSquad.Lmt.Application.Http`.
4. `dotnet restore` completed successfully after the import.
5. `AddGeneratedApi(baseUrl)` was registered in startup.
6. The API base URL is correct and reachable.
7. If using `AccessTokenAccessor`, the implementation is registered in DI.
8. The host application is consuming `Contracts` interfaces instead of Kiota classes.
9. No one manually edited the generated `.csproj` paths before the first successful build.

## Naming Convention

This module follows the embedded naming convention:

- `AlphaSquad.Lmt.Application.Contracts`
- `AlphaSquad.Lmt.Application.ApiClient`
- `AlphaSquad.Lmt.Application.Http`
