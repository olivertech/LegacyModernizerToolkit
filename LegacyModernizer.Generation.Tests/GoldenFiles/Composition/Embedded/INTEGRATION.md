# Integration Guide

This package was generated in `Embedded` mode for direct incorporation into an existing solution.

## Generated Projects

- `AlphaSquad.Lmt.Application.Contracts`
- `AlphaSquad.Lmt.Application.ApiClient`
- `AlphaSquad.Lmt.Application.Http`

## Recommended Integration Steps

1. Add the three generated projects to the target solution.
2. Reference `AlphaSquad.Lmt.Application.Http` from the consuming application.
3. Register the generated module with `AddGeneratedApi(baseUrl)`.
4. Consume only service and facade contracts from `AlphaSquad.Lmt.Application.Contracts`.
5. Do not reference Kiota types directly from the host application.

## Main Contracts

- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IApiFacade`
- `IAuthenticationService` / `AuthenticationService`

## Authentication Mode

This module was generated expecting an access token accessor strategy in the host application.
The next implementation phase will wire this mode into the generated HTTP layer.

## Naming Convention

This module follows the embedded naming convention:

- `AlphaSquad.Lmt.Application.Contracts`
- `AlphaSquad.Lmt.Application.ApiClient`
- `AlphaSquad.Lmt.Application.Http`
