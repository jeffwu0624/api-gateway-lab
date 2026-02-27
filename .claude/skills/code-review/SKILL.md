---
name: code-review
description: Review code for quality, Clean Architecture compliance, security, and .NET best practices. Use when reviewing changed files, a specific file, or a directory.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Bash, Task
argument-hint: [file, directory, or git diff target]
---

# Code Review

Review the specified code target: `$ARGUMENTS`

If no argument is provided, review all uncommitted changes (`git diff` + `git diff --cached` + untracked files).

---

## Review Process

1. **Identify scope** — determine which files to review based on the argument
2. **Read each file** thoroughly before commenting
3. **Check each category** below and report findings
4. **Output a structured report**

---

## Review Categories

### 1. Clean Architecture Compliance

- **Domain / Application `.csproj`**: Must have ZERO `<PackageReference>`
- **Application layer**: Must NOT `using Microsoft.EntityFrameworkCore` or `Microsoft.IdentityModel.*`
- **Controller**: Must only inject Use Cases — never Repository or DbContext
- **EF configuration**: Must use Fluent API in `Infrastructure/Configurations/` — Domain Entities must have NO data annotations
- **Dependency direction**: Domain <- Application <- Infrastructure <- API (no reverse references)

### 2. Security

- RSA keys: `using var rsa = RSA.Create()` — ensure Dispose
- No hardcoded secrets, connection strings, or keys in source code
- JWT: validate Issuer, Audience, Lifetime, SigningKey
- Passwords hashed with BCrypt (never stored in plaintext)
- Refresh tokens stored as SHA-256 hash, never raw
- No information leakage in error responses
- Rate limiting configured on public endpoints

### 3. .NET / C# Quality

- Nullable reference types handled correctly (`?` annotations, null checks)
- `async/await` used properly (no `.Result` or `.Wait()`)
- `CancellationToken` propagated through async chains
- No unused `using` statements
- Consistent naming: PascalCase for public members, camelCase for locals
- Records used for immutable DTOs
- `IReadOnlyList<T>` preferred over `List<T>` for public APIs

### 4. Entity Framework

- No `.Include()` on entities without navigation properties
- `DbUpdateConcurrencyException` caught and wrapped as `ConcurrencyException`
- SQLite compatibility: `IsConcurrencyToken()` instead of `IsRowVersion()`
- Shadow properties managed in Configuration classes, not Domain entities
- No `HasData()` — seeding via `DataSeeder.SeedAsync()` at startup

### 5. Error Handling

- Custom exceptions (`TokenReuseException`, `ConcurrencyException`) used instead of Wilson/IdentityModel exceptions
- Controller catch blocks distinguish exception types clearly
- Appropriate HTTP status codes (401, 403, 429, 503)
- No swallowed exceptions (empty catch blocks)

### 6. General

- No dead code or commented-out code
- No TODO/HACK/FIXME left unaddressed
- Methods reasonably sized (< 30 lines preferred)
- Single Responsibility — each class/method has one job

---

## Output Format

```markdown
# Code Review Report

## Summary
[1-2 sentence overview: what was reviewed and overall assessment]

## Findings

### [Category Name]

| Severity | File:Line | Issue | Suggestion |
|----------|-----------|-------|------------|
| HIGH/MED/LOW | path:line | description | fix |

## Positive Observations
[Things done well — reinforce good patterns]

## Action Items
- [ ] [Specific actionable fix with file path]
```

Severity definitions:
- **HIGH** — Security vulnerability, architecture violation, or runtime error
- **MED** — Code smell, performance issue, or maintainability concern
- **LOW** — Style, naming, or minor improvement suggestion

> For a deeper security-only audit, use `/security-review`.
