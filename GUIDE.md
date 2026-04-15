# Building a .NET REST API — End-to-End Guide

A practical, code-first guide covering every layer of a production-ready .NET 8 REST API: architecture, domain modelling, CQRS, authentication, testing, containerisation, CI/CD, and AWS deployment.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Project Structure](#2-project-structure)
3. [Domain Layer](#3-domain-layer)
4. [Application Layer — CQRS & Result Pattern](#4-application-layer--cqrs--result-pattern)
5. [Infrastructure Layer](#5-infrastructure-layer)
6. [API Layer](#6-api-layer)
7. [Authentication with JWT](#7-authentication-with-jwt)
8. [Validation](#8-validation)
9. [Error Handling & Problem Details](#9-error-handling--problem-details)
10. [Logging & Observability](#10-logging--observability)
11. [Health Checks](#11-health-checks)
12. [Testing Strategy](#12-testing-strategy)
13. [Containerisation with Docker](#13-containerisation-with-docker)
14. [CI/CD with GitHub Actions](#14-cicd-with-github-actions)
15. [Infrastructure as Code with Terraform (AWS)](#15-infrastructure-as-code-with-terraform-aws)
16. [Security Checklist](#16-security-checklist)
17. [Key Principles Summary](#17-key-principles-summary)

---

## 1. Architecture Overview

The project follows **Clean Architecture** (also known as Onion Architecture). The core rule is simple:

> **Dependencies always point inward. Outer layers know about inner layers; inner layers know nothing about outer layers.**

```
┌──────────────────────────────────────────────┐
│               API Layer                       │  HTTP, controllers, middleware, Swagger
├──────────────────────────────────────────────┤
│           Application Layer                   │  CQRS, use cases, validation, Result pattern
├──────────────────────────────────────────────┤
│             Domain Layer                      │  Entities, value objects, domain events, rules
├──────────────────────────────────────────────┤
│          Infrastructure Layer                 │  EF Core, PostgreSQL, JWT, repositories
└──────────────────────────────────────────────┘
```

**Why this matters:**
- The domain and application layers have **zero** framework dependencies — they are plain C# and can be tested in isolation without spinning up a database or HTTP server.
- You can swap PostgreSQL for another database, or swap JWT for OAuth, without touching a single line of business logic.
- Each layer has a single, well-defined responsibility.

---

## 2. Project Structure

```
src/
├── Contacts.Domain/          # No dependencies. Pure business rules.
├── Contacts.Application/     # Depends on Domain only.
├── Contacts.Infrastructure/  # Depends on Application (implements its interfaces).
└── Contacts.Api/             # Depends on Infrastructure. The composition root.

tests/
├── Contacts.UnitTests/       # Tests Domain + Application in isolation (fast, no I/O).
└── Contacts.IntegrationTests/# Tests the full HTTP stack with a real database.
```

**Rule of thumb for where code lives:**

| Question | Layer |
|---|---|
| Is this a business rule? | Domain |
| Is this a use case (what the system can do)? | Application |
| Does this talk to a database, file system, or external service? | Infrastructure |
| Is this HTTP-specific (routing, status codes, request/response)? | API |

---

## 3. Domain Layer

The domain layer is the heart of the application. It contains all business logic and **must never depend on anything outside itself**.

### 3.1 Aggregate Root

An aggregate root is the entry point into a cluster of related objects. All mutations go through it — never directly through child entities.

```csharp
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**Key rules:**
- No public setters on properties. State changes only happen via named methods (`Create`, `Update`, `Delete`).
- The aggregate raises domain events to announce what happened — it does not call other services directly.

### 3.2 Factory Methods instead of Constructors

Make the constructor `private`. Expose a `static Create(...)` factory method. This ensures an aggregate can never be constructed in an invalid state.

```csharp
public sealed class Contact : AggregateRoot
{
    private Contact() { }   // EF Core needs this; nothing else should use it

    public static Contact Create(string firstName, string lastName, Email email, ...)
    {
        // Validate inputs, set properties, raise ContactCreatedEvent
        var contact = new Contact { ... };
        contact.RaiseDomainEvent(new ContactCreatedEvent(contact.Id, contact.Email));
        return contact;
    }
}
```

### 3.3 Value Objects

Replace primitive types (`string`, `int`) with value objects to eliminate invalid states at compile time. A `string` can hold "not-an-email"; an `Email` value object cannot.

```csharp
public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email From(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsValidFormat(value))
            throw new ArgumentException($"'{value}' is not a valid email address.");

        return new Email(value.Trim().ToLowerInvariant());
    }
}
```

**Benefits:**
- Validation happens once, at the boundary.
- You cannot pass an unvalidated `string` where an `Email` is expected — the compiler enforces it.
- Equality is structural (`record`) not referential.

### 3.4 Strongly-Typed IDs

Prevent passing a `UserId` where a `ContactId` is expected by wrapping `Guid` in a named type:

```csharp
public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.NewGuid());
    public static ContactId From(Guid value) { ... }
}
```

This turns a runtime error (wrong ID passed) into a **compile-time error**.

### 3.5 Domain Events

Domain events record what happened inside an aggregate. They decouple side effects (sending emails, updating projections) from the core business logic.

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

public sealed record ContactCreatedEvent(ContactId ContactId, Email Email) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
```

Events are dispatched **after** the transaction commits to ensure consistency.

### 3.6 Domain Exceptions

Model expected business failures as explicit exception types, not generic `Exception`:

```csharp
public abstract class DomainException : Exception { ... }
public sealed class ContactNotFoundException : DomainException { ... }
public sealed class DuplicateEmailException : DomainException { ... }
```

These map to 4xx HTTP responses, never 5xx.

---

## 4. Application Layer — CQRS & Result Pattern

### 4.1 CQRS with MediatR

**Command Query Responsibility Segregation (CQRS)** separates write operations (commands) from read operations (queries). MediatR acts as an in-process message bus.

```
Request → MediatR → [Pipeline Behaviours] → Handler → Result
```

**Command** (write, returns a Result):
```csharp
public sealed record CreateContactCommand(
    string FirstName, string LastName, string Email, string? Phone, string? Organisation
) : IRequest<Result<Guid>>;
```

**Query** (read, returns data):
```csharp
public sealed record GetContactQuery(Guid Id) : IRequest<Result<ContactDto>>;
```

**Handler** (one per command/query):
```csharp
public sealed class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateContactCommand command, CancellationToken ct)
    {
        // 1. Check business rules
        // 2. Build domain objects
        // 3. Persist
        // 4. Return result
    }
}
```

**Benefits of CQRS:**
- Each handler has a single responsibility and is easy to test in isolation.
- Read and write models can evolve independently.
- Adding cross-cutting concerns (validation, logging) is done once in the pipeline.

### 4.2 The Result Pattern

Instead of throwing exceptions for expected business failures (not found, duplicate, etc.), return a `Result<T>` that forces callers to handle both the success and failure cases explicitly.

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }      // throws if accessed on failure
    public Error Error { get; }  // throws if accessed on success

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) => ...
}
```

The `Error` type carries a machine-readable code and a human-readable description:

```csharp
public sealed record Error(string Code, string Description)
{
    public static Error NotFound(string resource, object id) =>
        new($"{resource}.NotFound", $"{resource} with ID '{id}' was not found.");
}
```

**In a handler:**
```csharp
var contact = await _repo.GetByIdAsync(id);
if (contact is null)
    return Error.NotFound("Contact", id);   // implicit conversion to Result<T>
```

**In a controller:**
```csharp
var result = await _mediator.Send(new GetContactQuery(id));
return result.Match<IActionResult>(Ok, Problem);
```

### 4.3 Pipeline Behaviours

MediatR pipeline behaviours run before and after every handler — like middleware, but for your application logic.

**Validation behaviour** (runs FluentValidation automatically):
```csharp
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var failures = _validators.Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            return /* Result.Failure with validation errors */;

        return await next();
    }
}
```

**Logging behaviour** (structured timing logs on every request):
```csharp
public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        
        if (sw.Elapsed > TimeSpan.FromMilliseconds(500))
            _logger.LogWarning("Slow request: {Request} took {Ms}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}
```

Register both in DI:
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
});
```

### 4.4 Repository & Unit of Work Interfaces

Define repository contracts in the Application layer. The Infrastructure layer implements them. This is the **Dependency Inversion Principle** in action.

```csharp
// Application layer — defines the contract
public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(ContactId id, CancellationToken ct = default);
    Task AddAsync(Contact contact, CancellationToken ct = default);
    void Update(Contact contact);
}

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);
}
```

---

## 5. Infrastructure Layer

### 5.1 Entity Framework Core with PostgreSQL

Use EF Core's `IEntityTypeConfiguration<T>` to keep mapping configuration separate from domain entities:

```csharp
internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        // Map strongly-typed ID
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => ContactId.From(value));

        // Map Email value object
        builder.Property(c => c.Email)
            .HasConversion(e => e.Value, value => Email.From(value));

        // Unique index on email, excluding soft-deleted rows
        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Soft-delete global query filter — deleted records invisible to all queries
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
```

### 5.2 Dispatching Domain Events After Save

Override `SaveChangesAsync` in `DbContext` to dispatch domain events only after the transaction succeeds:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var aggregates = ChangeTracker.Entries()
        .Where(e => e.Entity is AggregateRoot)
        .Select(e => (AggregateRoot)e.Entity)
        .Where(a => a.DomainEvents.Count > 0)
        .ToList();

    var result = await base.SaveChangesAsync(ct);

    foreach (var aggregate in aggregates)
    {
        foreach (var domainEvent in aggregate.DomainEvents)
            await _publisher.Publish(domainEvent, ct);

        aggregate.ClearDomainEvents();
    }

    return result;
}
```

### 5.3 Repository Implementation

```csharp
internal sealed class ContactRepository : IContactRepository
{
    public async Task<Contact?> GetByIdAsync(ContactId id, CancellationToken ct) =>
        await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<(IReadOnlyList<Contact>, int)> ListAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Contacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, term) ||
                EF.Functions.ILike(c.Email.Value, term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
```

### 5.4 Migrations

```bash
# Add a migration
dotnet ef migrations add InitialCreate \
  --project src/Contacts.Infrastructure \
  --startup-project src/Contacts.Api

# Apply to database
dotnet ef database update \
  --project src/Contacts.Infrastructure \
  --startup-project src/Contacts.Api
```

---

## 6. API Layer

### 6.1 Controllers

Keep controllers thin — they should only:
1. Receive the HTTP request
2. Map it to a command or query
3. Send it via MediatR
4. Map the Result to an HTTP response

```csharp
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/contacts")]
[Authorize]
public sealed class ContactsController : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetContactQuery(id), ct);
        return result.Match<IActionResult>(Ok, Problem);
    }

    [HttpPost]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateContactCommand(...), ct);
        return result.Match<IActionResult>(
            id => CreatedAtAction(nameof(GetById), new { id }, new { id }),
            Problem);
    }
}
```

### 6.2 API Versioning

Always version your API from day one. Use URL segment versioning (most explicit, easiest to document):

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
});
```

### 6.3 Rate Limiting

Use the built-in ASP.NET 8 rate limiter to protect your API:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 100;
        cfg.QueueLimit = 0;
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

### 6.4 CORS

Configure CORS explicitly — avoid `AllowAnyOrigin` in production:

```csharp
builder.Services.AddCors(options =>
    options.AddPolicy("Api", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()));
```

---

## 7. Authentication with JWT

### 7.1 How it Works

```
Client                          API
  |                              |
  |-- POST /auth/token --------->|
  |                              |-- Validate credentials
  |<-- { accessToken: "..." } ---|
  |                              |
  |-- GET /contacts              |
  |   Authorization: Bearer <token>
  |                              |-- Validate JWT signature + expiry
  |<-- 200 OK { ... } ----------|
```

### 7.2 JWT Configuration

Store settings in `appsettings.json` (non-sensitive) and override secret via environment variable or Secrets Manager in production:

```json
{
  "Jwt": {
    "Secret": "CHANGE_ME_IN_PRODUCTION",
    "Issuer": "contacts-api",
    "Audience": "contacts-clients",
    "ExpiryMinutes": 60
  }
}
```

### 7.3 Token Generation

```csharp
public string GenerateToken(string userId, string email, string[] roles)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, userId),
        new(JwtRegisteredClaimNames.Email, email),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };
    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var token = new JwtSecurityToken(
        issuer: _settings.Issuer,
        audience: _settings.Audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### 7.4 Token Validation

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew = TimeSpan.FromSeconds(30)   // Tight skew tolerance
        };
    });
```

### 7.5 Production Recommendation

In production, **do not issue tokens yourself**. Delegate to an Identity Provider:
- **AWS Cognito** — managed, integrates with IAM
- **Auth0** — developer-friendly, generous free tier
- **Microsoft Entra ID** (formerly Azure AD) — enterprise SSO

Your API only needs to validate the JWT the IdP issues. Remove the `/auth/token` endpoint entirely.

---

## 8. Validation

Use **FluentValidation** for all inbound request/command validation.

### 8.1 Validator

```csharp
public sealed class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(254);

        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("Phone must be in E.164 format (e.g. +6421234567).")
            .When(x => x.Phone is not null);
    }
}
```

### 8.2 Where to Validate

There are two levels of validation and both are needed:

| Level | Where | What it catches |
|---|---|---|
| **Input validation** | FluentValidation in Application layer | Missing fields, wrong formats, length limits |
| **Domain validation** | Value object constructors + aggregate methods | Business rule violations |

FluentValidation catches bad input early and produces clear user-facing error messages. Value objects enforce correctness throughout the domain — even if data comes from a source that bypassed the API.

---

## 9. Error Handling & Problem Details

### 9.1 RFC 7807 — Problem Details

All error responses must follow [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807). This gives API consumers a consistent, machine-readable error format:

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Contact.NotFound",
  "status": 404,
  "detail": "Contact with ID 'abc-123' was not found.",
  "instance": "/api/v1/contacts/abc-123",
  "correlationId": "7f3a2b1c-..."
}
```

### 9.2 Mapping Errors to HTTP Status Codes

```csharp
private IActionResult Problem(Error error)
{
    var statusCode = error.Code switch
    {
        var c when c.EndsWith(".NotFound")     => 404,
        var c when c.StartsWith("Validation")  => 400,
        var c when c.Contains("Duplicate")     => 409,
        "Auth.Unauthorised"                    => 403,
        _                                      => 500
    };

    return Problem(
        type: $"https://httpstatuses.com/{statusCode}",
        title: error.Code,
        detail: error.Description,
        statusCode: statusCode);
}
```

### 9.3 Global Exception Handler Middleware

Catches any unhandled exception and returns a 500 Problem Details response without leaking stack traces in production:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception for {Method} {Path}", ...);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = "Internal Server Error",
            Status = 500,
            Detail = _env.IsDevelopment() ? ex.Message : "An unexpected error occurred."
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
```

---

## 10. Logging & Observability

### 10.1 Structured Logging with Serilog

Structured logging attaches key-value properties to each log entry, making them searchable and filterable in tools like CloudWatch, Datadog, or Elastic.

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

### 10.2 Correlation IDs

Every request must carry a correlation ID so that a single user request can be traced across multiple log entries and services:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();

    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await _next(context);
    }
}
```

### 10.3 What to Log

| Level | Use for |
|---|---|
| `Debug` | Development-only diagnostics |
| `Information` | Normal operations (request handled, record created) |
| `Warning` | Slow queries, retries, unexpected but recoverable situations |
| `Error` | Unhandled exceptions, failed operations |

**Never log:** passwords, tokens, full credit card numbers, or PII unless required by compliance.

---

## 11. Health Checks

Expose health endpoints for liveness and readiness probes (required by Kubernetes and ECS):

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

// Map endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new() {
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new() {
    Predicate = r => r.Tags.Contains("ready")
});
```

**Distinction:**
- `/health/live` — Is the process running? (No dependency checks. Always fast.)
- `/health/ready` — Can the process serve traffic? (Checks DB, external services.)

---

## 12. Testing Strategy

### 12.1 Testing Pyramid

```
        /\
       /  \
      / E2E \         Few — expensive, slow, high confidence
     /--------\
    / Integration\    Some — real DB, real HTTP, medium speed
   /——————————————\
  /   Unit Tests   \  Many — fast, isolated, no I/O
 /------------------\
```

### 12.2 Unit Tests

Test business logic in complete isolation using NSubstitute for mocks and FluentAssertions for readable assertions:

```csharp
public sealed class CreateContactCommandHandlerTests
{
    private readonly IContactRepository _repo = Substitute.For<IContactRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithId()
    {
        _repo.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>())
             .Returns((Contact?)null);

        var result = await new CreateContactCommandHandler(_repo, _uow)
            .Handle(new CreateContactCommand("Jane", "Doe", "jane@example.com", null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
```

**What to unit test:**
- All domain entity methods (Create, Update, Delete)
- All value object construction (valid and invalid inputs)
- All command and query handlers
- All FluentValidation validators
- The Result type itself

### 12.3 Integration Tests with Testcontainers

Testcontainers spins up a real PostgreSQL container in Docker for your tests — no mocking of the database, no shared test state:

```csharp
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("contacts_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace production DB with test container
            services.RemoveDbContext<AppDbContext>();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            // Run migrations
            var sp = services.BuildServiceProvider();
            sp.CreateScope().ServiceProvider
              .GetRequiredService<AppDbContext>().Database.Migrate();
        });
    }
}
```

**What to integration test:**
- Full HTTP request → response cycle
- Authentication (valid token, missing token, wrong role)
- Business flows (create → get, create → delete → get returns 404)
- Pagination
- Health check endpoints

### 12.4 Test Naming Convention

Follow the pattern: `MethodName_StateUnderTest_ExpectedBehaviour`

```csharp
public async Task CreateContact_WithDuplicateEmail_Returns409()
public async Task GetContact_WithUnknownId_Returns404()
public void Validate_WithEmptyFirstName_ShouldHaveError()
```

### 12.5 Running Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" \
            --results-directory ./coverage \
            -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

---

## 13. Containerisation with Docker

### 13.1 Multi-Stage Dockerfile

Use a multi-stage build to keep the production image small and free of build tools:

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["src/Contacts.Api/Contacts.Api.csproj", "src/Contacts.Api/"]
# ... copy other csproj files for layer cache
RUN dotnet restore "src/Contacts.Api/Contacts.Api.csproj"
COPY . .
RUN dotnet publish "src/Contacts.Api/Contacts.Api.csproj" \
    --configuration Release --no-restore -o /app/publish

# Stage 2: Runtime only
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
WORKDIR /app
USER appuser          # Non-root for security
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build --chown=appuser:appgroup /app/publish .
HEALTHCHECK CMD wget -qO- http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Contacts.Api.dll"]
```

**Key practices:**
- Copy `.csproj` files and restore **before** copying source — Docker layer cache means `dotnet restore` only re-runs when dependencies change, not on every code change.
- Use `alpine` base images (smallest attack surface, ~100MB vs ~300MB).
- Run as a non-root user.
- Add a `HEALTHCHECK` instruction.
- TLS termination at the load balancer — container only speaks HTTP internally.

### 13.2 Docker Compose for Local Development

```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: contacts
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s

  api:
    build: .
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;..."
    depends_on:
      postgres:
        condition: service_healthy
```

```bash
docker compose up        # Start everything
docker compose up -d     # Start in background
docker compose logs -f   # Follow logs
docker compose down      # Stop and remove containers
```

---

## 14. CI/CD with GitHub Actions

### 14.1 Pipeline Overview

```
Push to PR / develop
         │
         ▼
   ┌─────────────┐      ┌──────────────────┐      ┌────────────┐
   │ Build + Unit │─────▶│ Integration Tests │─────▶│ Trivy Scan │
   │    Tests    │      │  (Testcontainers) │      │  (SARIF)   │
   └─────────────┘      └──────────────────┘      └────────────┘
                                                          │
                                               push to develop passes
                                                          │
                                                          ▼
                                               ┌────────────────────┐
                                               │  Build Docker image │
                                               │  Push to ECR        │
                                               │  Scan image (Trivy) │
                                               │  Deploy → Staging   │
                                               └──────────┬─────────┘
                                                          │
                                                    tag v*.*.*
                                                          │
                                                          ▼
                                               ┌────────────────────┐
                                               │  Manual approval    │
                                               │  Promote image tag  │
                                               │  Deploy → Production│
                                               │  Blue/green deploy  │
                                               │  Create GH Release  │
                                               └────────────────────┘
```

### 14.2 CI Workflow

```yaml
name: CI
on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test tests/Contacts.UnitTests --no-build --configuration Release
```

### 14.3 AWS Authentication — OIDC (No Long-Lived Keys)

Never store long-lived AWS access keys in GitHub secrets. Use OIDC to get short-lived credentials:

```yaml
permissions:
  id-token: write
  contents: read

- name: Configure AWS credentials
  uses: aws-actions/configure-aws-credentials@v4
  with:
    role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
    aws-region: ap-southeast-2
```

The IAM role trusts GitHub's OIDC provider and is scoped to specific repositories and branches.

### 14.4 CD — Build and Push to ECR

```yaml
- name: Login to ECR
  id: login-ecr
  uses: aws-actions/amazon-ecr-login@v2

- name: Build and push
  env:
    ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}
    IMAGE_TAG: ${{ github.sha }}
  run: |
    docker build -t "$ECR_REGISTRY/contacts-api:$IMAGE_TAG" .
    docker push "$ECR_REGISTRY/contacts-api:$IMAGE_TAG"
```

### 14.5 Deployment Strategy

| Environment | Trigger | Strategy |
|---|---|---|
| Staging | Push to `develop` | Rolling update |
| Production | `v*.*.*` tag | Blue/green via AWS CodeDeploy |

Blue/green deployment:
- New version deployed to a new set of tasks (green).
- Traffic shifted gradually; old tasks (blue) remain running.
- Automatic rollback if health checks fail.
- Zero downtime.

---

## 15. Infrastructure as Code with Terraform (AWS)

### 15.1 Architecture

```
Internet
    │
    ▼
Application Load Balancer (HTTPS 443)   ← Public subnet
    │
    ▼
ECS Fargate Tasks                        ← Private subnet
    │
    ▼
Aurora PostgreSQL Serverless v2          ← Private subnet (RDS subnet group)
```

### 15.2 Key Resource Decisions

**ECR (Container Registry):**
```hcl
resource "aws_ecr_repository" "api" {
  name                 = "contacts-api"
  image_tag_mutability = "IMMUTABLE"  # Tags cannot be overwritten — full traceability

  image_scanning_configuration {
    scan_on_push = true   # Automatic vulnerability scan
  }
}
```

**ECS Fargate (no servers to manage):**
```hcl
resource "aws_ecs_service" "api" {
  name            = "contacts-api"
  launch_type     = "FARGATE"
  desired_count   = 2          # Always 2 tasks for HA

  deployment_circuit_breaker {
    enable   = true
    rollback = true   # Auto-rollback on failure
  }
}
```

**Aurora Serverless v2 (scales to zero on staging):**
```hcl
resource "aws_rds_cluster" "main" {
  engine      = "aurora-postgresql"
  engine_mode = "provisioned"

  serverlessv2_scaling_configuration {
    min_capacity = 0.5    # Cost-efficient on non-prod
    max_capacity = 8.0    # Scales automatically under load
  }
}
```

**Secrets Manager (never commit secrets):**
```hcl
resource "aws_secretsmanager_secret" "jwt_secret" {
  name = "contacts/production/jwt-secret"
}
```

Inject secrets into ECS tasks as environment variables at runtime — they're never baked into the image:
```hcl
secrets = [
  {
    name      = "Jwt__Secret"
    valueFrom = aws_secretsmanager_secret.jwt_secret.arn
  }
]
```

### 15.3 Terraform Workflow

```bash
# Initialise (first time, or after provider changes)
terraform init

# Preview changes
terraform plan -var="environment=staging" -var="jwt_secret=$JWT_SECRET"

# Apply
terraform apply -var-file=staging.tfvars

# Destroy (staging only — never run on production without care)
terraform destroy -var-file=staging.tfvars
```

### 15.4 Remote State

Always store Terraform state remotely, not locally:

```hcl
terraform {
  backend "s3" {
    bucket         = "my-tf-state"
    key            = "contacts/terraform.tfstate"
    region         = "ap-southeast-2"
    encrypt        = true
    dynamodb_table = "my-tf-lock"   # Prevents concurrent applies
  }
}
```

---

## 16. Security Checklist

### OWASP Top 10 Mitigations

| Risk | Mitigation in this project |
|---|---|
| **Injection** | EF Core parameterised queries; no raw SQL string concatenation |
| **Broken Authentication** | JWT validated on every request; `ClockSkew` minimised; short token lifetime |
| **Sensitive Data Exposure** | Secrets in AWS Secrets Manager; never in code or env files; HTTPS enforced at ALB |
| **XML External Entities** | Not applicable (JSON only) |
| **Broken Access Control** | `[Authorize]` on all resource endpoints; roles on sensitive operations |
| **Security Misconfiguration** | No stack traces in production responses; specific CORS origins; least-privilege IAM |
| **XSS** | Not applicable (API, no HTML rendering) |
| **Insecure Deserialisation** | `System.Text.Json` with strict options; no `[FromBody]` with arbitrary types |
| **Vulnerable Components** | Trivy scans image and repo in CI; ECR scan-on-push; Dependabot alerts |
| **Insufficient Logging** | Structured logs on every request; slow query warnings; failed auth attempts logged |

### Additional Practices

- **Input validation at every boundary** — FluentValidation on inbound commands; value objects inside the domain.
- **Immutable images** — ECR tags cannot be overwritten. Deploy by SHA, not `latest`.
- **Non-root container user** — Container runs as `appuser`, not root.
- **Private subnets** — ECS tasks and RDS have no public IPs.
- **Security groups** — ECS tasks only accept traffic from the ALB; RDS only accepts traffic from ECS tasks.
- **TLS 1.3** — Enforced via ALB security policy `ELBSecurityPolicy-TLS13-1-2-2021-06`.

---

## 17. Key Principles Summary

| Principle | Applied as |
|---|---|
| **Single Responsibility** | One handler per command/query; one validator per command |
| **Open/Closed** | New features = new handlers/validators, not modifications to existing ones |
| **Liskov Substitution** | Repository interfaces satisfied by any concrete implementation |
| **Interface Segregation** | `IContactRepository` only exposes methods the domain actually needs |
| **Dependency Inversion** | Application layer defines interfaces; Infrastructure implements them |
| **Fail fast** | Value objects throw on construction; validators run before handlers |
| **Explicit over implicit** | Result pattern forces callers to handle failures; no silent swallowing of errors |
| **Immutability** | Value objects are records; aggregate properties have no public setters |
| **Testability** | Domain and Application have zero infrastructure dependencies; fully unit-testable |
| **Observability** | Every request has a correlation ID, structured log entry, and duration metric |

---

*Based on the Contacts REST API project — a complete, buildable reference implementation.*
