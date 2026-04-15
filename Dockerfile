# ── Stage 1: restore & build ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy project files first to leverage Docker layer cache for package restore
COPY ["src/Contacts.Api/Contacts.Api.csproj",            "src/Contacts.Api/"]
COPY ["src/Contacts.Application/Contacts.Application.csproj", "src/Contacts.Application/"]
COPY ["src/Contacts.Domain/Contacts.Domain.csproj",      "src/Contacts.Domain/"]
COPY ["src/Contacts.Infrastructure/Contacts.Infrastructure.csproj", "src/Contacts.Infrastructure/"]

RUN dotnet restore "src/Contacts.Api/Contacts.Api.csproj" \
    --runtime linux-musl-x64

# Copy remaining sources and publish
COPY . .
RUN dotnet publish "src/Contacts.Api/Contacts.Api.csproj" \
    --configuration Release \
    --runtime linux-musl-x64 \
    --self-contained false \
    --no-restore \
    -o /app/publish \
    /p:GenerateDocumentationFile=true \
    /p:TreatWarningsAsErrors=true

# ── Stage 2: final runtime image ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

# Security: run as non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
WORKDIR /app
USER appuser

# Expose HTTP only; TLS termination handled by load balancer in production
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build --chown=appuser:appgroup /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Contacts.Api.dll"]
