# Contacts REST API

A production-ready .NET 8 REST API demonstrating end-to-end engineering best practices:
**Clean Architecture · CQRS · JWT Auth · PostgreSQL · Docker · GitHub Actions CI/CD · AWS ECS**

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                        API Layer                     │  ← HTTP, Swagger, versioning, middleware
│  Contacts.Api                                    │
├─────────────────────────────────────────────────────┤
│                   Application Layer                  │  ← CQRS (MediatR), Result pattern, validators
│  Contacts.Application                           │
├─────────────────────────────────────────────────────┤
│                    Domain Layer                      │  ← Entities, value objects, domain events
│  Contacts.Domain                                │
├─────────────────────────────────────────────────────┤
│                 Infrastructure Layer                 │  ← EF Core + PostgreSQL, JWT, repositories
│  Contacts.Infrastructure                        │
└─────────────────────────────────────────────────────┘
```

### Key patterns used

| Practice | Implementation |
|---|---|
| Clean Architecture | Strict dependency rule: outer layers depend inward only |
| Domain-Driven Design | Aggregate root, value objects (`Email`, `PhoneNumber`), domain events |
| CQRS | Commands and queries separated via MediatR |
| Result pattern | `Result<T>` — no exception throwing for expected business failures |
| Pipeline behaviours | Automatic validation + structured logging on every MediatR request |
| Soft deletes | `IsDeleted` flag with EF global query filter |
| Outbox / domain events | Dispatched after `SaveChangesAsync` via MediatR `IPublisher` |
| RFC 7807 Problem Details | Consistent error responses across all endpoints |
| API Versioning | URL segment + header (`X-Api-Version`) |
| Rate limiting | `AddRateLimiter` (ASP.NET 8 built-in, 100 req/min) |
| Health checks | `/health`, `/health/live`, `/health/ready` |
| Correlation IDs | `X-Correlation-Id` threaded through logs and responses |
| Structured logging | Serilog with log context enrichment |
| Containerisation | Multi-stage Docker build, non-root user |
| Integration tests | Testcontainers (real PostgreSQL in Docker) |
| Security scanning | Trivy in CI, ECR scan-on-push, OWASP dependency check |
| Zero-downtime deploy | Blue/green via AWS CodeDeploy on ECS (production) |
| Autoscaling | ECS Application Auto Scaling on CPU utilisation |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [PostgreSQL 16](https://www.postgresql.org/) *(or use docker-compose)*

---

## Quick start (local)

### 1 — Start PostgreSQL with Docker Compose

```bash
docker compose up postgres -d
```

### 2 — Run the API

```bash
cd src/Contacts.Api
dotnet run
```

Swagger UI is available at **http://localhost:8080** (Development environment only).

### 3 — Get a token

```bash
curl -s -X POST http://localhost:8080/api/v1/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin1234!"}' \
  | jq .accessToken
```

### 4 — Call a protected endpoint

```bash
TOKEN="<paste token here>"

# Create a contact
curl -s -X POST http://localhost:8080/api/v1/contacts \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "lastName":  "Doe",
    "email":     "jane.doe@example.com",
    "phone":     "+6421234567",
    "organisation": "Acme Ltd"
  }' | jq

# List contacts
curl -s http://localhost:8080/api/v1/contacts \
  -H "Authorization: Bearer $TOKEN" | jq
```

---

## Running tests

```bash
# Unit tests
dotnet test tests/Contacts.UnitTests

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/Contacts.IntegrationTests

# All tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Database migrations

```bash
# From the repo root
dotnet ef migrations add <MigrationName> \
  --project src/Contacts.Infrastructure \
  --startup-project src/Contacts.Api

dotnet ef database update \
  --project src/Contacts.Infrastructure \
  --startup-project src/Contacts.Api
```

---

## CI/CD pipeline

```
PR / push to develop
        │
        ▼
┌───────────────┐    ┌─────────────────┐    ┌──────────────┐
│  Build + Unit │───▶│   Integration   │───▶│  Trivy scan  │
│     Tests     │    │     Tests       │    │   (SARIF)    │
└───────────────┘    └─────────────────┘    └──────┬───────┘
                                                   │
                                     push to develop passes
                                                   │
                                                   ▼
                                     ┌─────────────────────┐
                                     │  Build Docker image  │
                                     │  Push to ECR         │
                                     │  Deploy → Staging    │
                                     │  (rolling update)    │
                                     └──────────┬──────────┘
                                                │
                                      git tag  v*.*.*
                                                │
                                                ▼
                                     ┌─────────────────────┐
                                     │  Manual approval     │
                                     │  Promote image tag   │
                                     │  Deploy → Production │
                                     │  (blue/green)        │
                                     │  Create GH Release   │
                                     └─────────────────────┘
```

### Required GitHub secrets

| Secret | Description |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | IAM role ARN for staging OIDC auth |
| `AWS_DEPLOY_ROLE_ARN_PROD` | IAM role ARN for production OIDC auth |
| `STAGING_API_URL` | Staging ALB URL for smoke test |
| `PRODUCTION_API_URL` | Production ALB URL for smoke test |

---

## AWS infrastructure

Managed with Terraform in `infrastructure/`.

```
VPC (10.0.0.0/16)
├── Public subnets  → Application Load Balancer (HTTPS 443)
└── Private subnets → ECS Fargate tasks + Aurora PostgreSQL

ECR  → Immutable image tags, scan on push
ECS  → Fargate, CloudWatch Container Insights, auto-scaling 2–10 tasks
RDS  → Aurora PostgreSQL Serverless v2 (auto-scales ACUs)
SM   → JWT secret + DB credentials managed by Secrets Manager
ALB  → TLS 1.3, access logs to S3
```

### Deploy with Terraform

```bash
cd infrastructure

terraform init \
  -backend-config="bucket=<your-state-bucket>" \
  -backend-config="region=ap-southeast-2"

terraform plan \
  -var="environment=staging" \
  -var="jwt_secret=<secret>" \
  -var="acm_certificate_arn=<arn>"

terraform apply -var-file=staging.tfvars
```

---

## API reference

Full OpenAPI specification is served at `/swagger/v1/swagger.json` in Development.

| Method | Path | Description |
|---|---|---|
| POST | `/api/v1/auth/token` | Get JWT token |
| GET | `/api/v1/contacts` | List contacts (paginated) |
| POST | `/api/v1/contacts` | Create contact |
| GET | `/api/v1/contacts/{id}` | Get contact by ID |
| PUT | `/api/v1/contacts/{id}` | Update contact |
| DELETE | `/api/v1/contacts/{id}` | Soft-delete contact |
| GET | `/health` | Full health report |
| GET | `/health/live` | Liveness probe (no dependencies) |
| GET | `/health/ready` | Readiness probe (DB check) |

---

## Security notes

- JWT tokens must be kept confidential; never log them.
- In production, replace the demo `DemoUsers` section with a real IdP (AWS Cognito, Auth0, etc.).
- All secrets are stored in AWS Secrets Manager — never committed to source control.
- Database runs in private subnets with no public access.
- The container runs as a non-root user (`appuser`).
