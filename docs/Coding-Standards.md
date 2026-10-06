# Coding standards

These rules match the code that is in the repository.

- .NET 9, nullable reference types, and warnings as errors.
- MediatR stays on 12.x. FluentAssertions stays on 7.x.
- A use case is a command or query, a handler, and a validator when input needs one.
- Controllers are the HTTP adapter. Handlers own the outcome.
- Depend inward. Domain does not reference EF Core, ASP.NET Core, or Infrastructure.
- Constructor injection, `async`/`await`, and `CancellationToken` for I/O.
- Liquibase owns schema changes. Do not add EF Core migrations.
- Do not log passwords, hashes, tokens, or API keys.
- Do not trust a client tenant id unless the permission check proves membership in that tenant.
- Keep new files under 300 lines.
- Do not add a product behavior that was not requested.
