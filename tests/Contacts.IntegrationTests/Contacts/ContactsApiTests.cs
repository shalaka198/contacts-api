using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Contacts.Application.Common;
using Contacts.Application.Contacts.Queries;
using Contacts.IntegrationTests.Fixtures;
using Xunit;

namespace Contacts.IntegrationTests.Contacts;

[Collection("Integration")]
public sealed class ContactsApiTests : IAsyncLifetime
{
    private readonly ApiWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public ContactsApiTests(ApiWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync() =>
        _client = await _factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ── POST /api/v1/contacts ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateContact_WithValidBody_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/contacts", new
        {
            firstName = "Jane",
            lastName = "Doe",
            email = $"jane.doe+{Guid.NewGuid():N}@example.com",
            phone = "+6421234567",
            organisation = "Acme Ltd"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateContact_WithDuplicateEmail_Returns409()
    {
        var email = $"dup+{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/contacts",
            new { firstName = "A", lastName = "B", email, phone = (string?)null, organisation = (string?)null });

        var response = await _client.PostAsJsonAsync("/api/v1/contacts",
            new { firstName = "C", lastName = "D", email, phone = (string?)null, organisation = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateContact_WithMissingEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/contacts", new
        {
            firstName = "Jane",
            lastName = "Doe",
            email = "",   // invalid
            phone = (string?)null,
            organisation = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/v1/contacts/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetContact_WithExistingId_Returns200WithContact()
    {
        var email = $"get+{Guid.NewGuid():N}@example.com";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/contacts",
            new { firstName = "Jane", lastName = "Doe", email, phone = (string?)null, organisation = (string?)null });

        var created = await createResponse.Content.ReadFromJsonAsync<IdBody>();
        var response = await _client.GetAsync($"/api/v1/contacts/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await response.Content.ReadFromJsonAsync<ContactDto>();
        contact!.Email.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task GetContact_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/contacts/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/v1/contacts ──────────────────────────────────────────────────

    [Fact]
    public async Task ListContacts_Returns200WithPagedResult()
    {
        var response = await _client.GetAsync("/api/v1/contacts?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>();
        paged.Should().NotBeNull();
        paged!.Items.Should().NotBeNull();
    }

    // ── PUT /api/v1/contacts/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateContact_WithValidData_Returns204()
    {
        var email = $"upd+{Guid.NewGuid():N}@example.com";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/contacts",
            new { firstName = "Jane", lastName = "Doe", email, phone = (string?)null, organisation = (string?)null });

        var created = await createResponse.Content.ReadFromJsonAsync<IdBody>();

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/contacts/{created!.Id}", new
        {
            firstName = "Janet",
            lastName = "Doe",
            email,
            phone = "+6499887766",
            organisation = "Updated Corp"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── DELETE /api/v1/contacts/{id} ──────────────────────────────────────────

    [Fact]
    public async Task DeleteContact_WithExistingId_Returns204_ThenGetReturns404()
    {
        var email = $"del+{Guid.NewGuid():N}@example.com";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/contacts",
            new { firstName = "Jane", lastName = "Doe", email, phone = (string?)null, organisation = (string?)null });

        var created = await createResponse.Content.ReadFromJsonAsync<IdBody>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/contacts/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/contacts/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetContacts_WithoutToken_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync("/api/v1/contacts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Health check ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_Live_Returns200()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record IdBody(Guid Id);
}
