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
- `AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor`
- `IAuthenticationService` / `AuthenticationService`

## Authentication Mode

This module was generated with access token resolution delegated to the host application.
Register an implementation of `IAccessTokenAccessor` before calling `AddGeneratedApi(baseUrl)`.

## Naming Convention

This module follows the embedded naming convention:

- `AlphaSquad.Lmt.Application.Contracts`
- `AlphaSquad.Lmt.Application.ApiClient`
- `AlphaSquad.Lmt.Application.Http`
