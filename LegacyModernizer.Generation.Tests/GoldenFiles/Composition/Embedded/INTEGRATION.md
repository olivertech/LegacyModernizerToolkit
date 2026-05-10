# Integration Guide

This package was generated in `Embedded` mode for direct incorporation into an existing solution.

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

## Step 1 - Add The Generated Projects To Your Existing Solution

Open a terminal in the root folder of the host solution and add the generated projects to the `.sln`.

The generated projects already include explicit package versions and opt out of central package version management in `Embedded` mode.
This is intentional, so the imported module can compile more predictably even when the host solution has its own package policy.

Example commands:

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

## Step 2 - Reference The HTTP Project From The Consuming Application

The host application usually needs to reference only the HTTP project directly, because it already depends on `Contracts` and `ApiClient`.

Typical consuming applications:

- MVC / Razor project
- Admin dashboard
- Web API that orchestrates another API
- MAUI host project
- Blazor host project

Example command:

```powershell
dotnet add .\src\YourHostProject\YourHostProject.csproj reference .\src\AlphaSquad.Lmt.Application.Http\AlphaSquad.Lmt.Application.Http.csproj
```

## Step 3 - Register The Generated Module In Dependency Injection

In the startup file of the host application, register the generated module using:

- `AlphaSquad.Lmt.Application.Http.DependencyInjection.ServiceCollectionExtensions`
- method: `AddGeneratedApi(baseUrl)`

Typical files:

- `Program.cs`
- `Startup.cs`

Example:

```csharp
using AlphaSquad.Lmt.Application.Http.DependencyInjection;

builder.Services.AddGeneratedApi("https://api.example.com");
```

If your API base URL comes from configuration, prefer something like:

```csharp
using AlphaSquad.Lmt.Application.Http.DependencyInjection;

var apiBaseUrl = builder.Configuration["Apis:MainApi:BaseUrl"]
                 ?? throw new InvalidOperationException("API base URL was not configured.");

builder.Services.AddGeneratedApi(apiBaseUrl);
```

## Authentication Setup

This output was generated with `AccessTokenAccessor` mode.
That means the host application must provide an implementation of:

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`

### Where to create it

Create the implementation inside the consuming application project.

Examples:

- `src/AlphaSquad.Admin/Security/AccessTokenAccessor.cs`
- `src/AlphaSquad.Web/Security/AccessTokenAccessor.cs`
- `src/AlphaSquad.App/Authentication/AccessTokenAccessor.cs`

### Example implementation

```csharp
using System.Threading;
using System.Threading.Tasks;
using AlphaSquad.Lmt.Application.Contracts.Interfaces;

namespace AlphaSquad.Admin.Security;

public sealed class AccessTokenAccessor : IAccessTokenAccessor
{
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Replace this with the real token source used by your application.
        // Examples:
        // - HttpContext session
        // - authentication cookie
        // - token cache
        // - secure local storage
        return Task.FromResult<string?>("your-access-token");
    }
}
```

### Where to register it

Register this implementation in the startup file of the consuming application.

Examples:

- `Program.cs`
- `Startup.cs`

```csharp
using AlphaSquad.Lmt.Application.Contracts.Interfaces;
using AlphaSquad.Admin.Security;

builder.Services.AddScoped<IAccessTokenAccessor, AccessTokenAccessor>();
```

## Step 4 - Consume Only The Generated Contracts And Services

After registration, the consuming application should request dependencies from DI using the generated interfaces.

Prefer using:

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IApiFacade`
- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`
- `IAuthenticationService` / `AuthenticationService`

Do not consume:

- raw Kiota request builders
- Kiota models directly in pages or controllers
- `AlphaSquad.Lmt.Application.ApiClient` as a public API surface

## Step 5 - Example Of Consumption In The Host Application

### Example in a controller or page model

```csharp
using Microsoft.AspNetCore.Mvc;

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

## Step 6 - Validate The Integration

After adding the projects and registering DI, run:

```powershell
dotnet build
```

If the host application already has tests, also run:

```powershell
dotnet test
```

## Main Contracts

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IApiFacade`
- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`
- `IAuthenticationService` / `AuthenticationService`

## Troubleshooting

If the integration does not work as expected, verify these points first:

1. The three generated projects were added to the target solution.
2. The consuming application references `AlphaSquad.Lmt.Application.Http`.
3. `AddGeneratedApi(baseUrl)` was registered in startup.
4. The API base URL is correct and reachable.
5. If using `AccessTokenAccessor`, the implementation is registered in DI.
6. The host application is consuming `Contracts` interfaces instead of Kiota classes.

## Naming Convention

This module follows the embedded naming convention:

- `AlphaSquad.Lmt.Application.Contracts`
- `AlphaSquad.Lmt.Application.ApiClient`
- `AlphaSquad.Lmt.Application.Http`
