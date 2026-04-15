using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Contacts.Application.Common;
using Contacts.Application.Contacts.Commands.CreateContact;
using Contacts.Application.Contacts.Commands.DeleteContact;
using Contacts.Application.Contacts.Commands.UpdateContact;
using Contacts.Application.Contacts.Queries;
using Contacts.Application.Contacts.Queries.GetContact;
using Contacts.Application.Contacts.Queries.ListContacts;

namespace Contacts.Api.Controllers;

/// <summary>
/// Manages Contacts resources.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/contacts")]
[Authorize]
[EnableRateLimiting("fixed")]
[Produces("application/json")]
public sealed class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContactsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Returns a paginated list of contacts.
    /// </summary>
    /// <param name="search">Optional search term filtered across name, email and organisation.</param>
    /// <param name="page">Page number (1-based, default: 1).</param>
    /// <param name="pageSize">Items per page (1–100, default: 20).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListContactsQuery(search, page, pageSize), cancellationToken);

        return result.Match<IActionResult>(Ok, Problem);
    }

    /// <summary>
    /// Returns a single contact by ID.
    /// </summary>
    /// <param name="id">The contact's unique identifier.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetContactQuery(id), cancellationToken);
        return result.Match<IActionResult>(Ok, Problem);
    }

    /// <summary>
    /// Creates a new contact.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateContactCommand(
            request.FirstName, request.LastName, request.Email,
            request.Phone, request.Organisation);

        var result = await _mediator.Send(command, cancellationToken);

        return result.Match<IActionResult>(
            id => CreatedAtAction(nameof(GetById), new { id }, new { id }),
            Problem);
    }

    /// <summary>
    /// Replaces an existing contact (full update).
    /// </summary>
    /// <param name="id">The contact's unique identifier.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateContactCommand(
            id, request.FirstName, request.LastName, request.Email,
            request.Phone, request.Organisation);

        var result = await _mediator.Send(command, cancellationToken);
        return result.Match<IActionResult>(_ => NoContent(), Problem);
    }

    /// <summary>
    /// Soft-deletes a contact.
    /// </summary>
    /// <param name="id">The contact's unique identifier.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeleteContactCommand(id), cancellationToken);
        return result.Match<IActionResult>(_ => NoContent(), Problem);
    }

    // ── Private helper: map Error to RFC 7807 ProblemDetails ─────────────────
    private IActionResult Problem(Error error)
    {
        var (statusCode, type) = error.Code switch
        {
            var c when c.EndsWith(".NotFound") => (StatusCodes.Status404NotFound, "https://httpstatuses.com/404"),
            var c when c.StartsWith("Validation") => (StatusCodes.Status400BadRequest, "https://httpstatuses.com/400"),
            var c when c.Contains("DuplicateEmail") || c.Contains("Conflict") => (StatusCodes.Status409Conflict, "https://httpstatuses.com/409"),
            "Auth.Unauthorised" => (StatusCodes.Status403Forbidden, "https://httpstatuses.com/403"),
            _ => (StatusCodes.Status500InternalServerError, "https://httpstatuses.com/500")
        };

        return Problem(
            type: type,
            title: error.Code,
            detail: error.Description,
            statusCode: statusCode,
            instance: HttpContext.Request.Path);
    }
}

// ── Request DTOs (inbound contracts) ─────────────────────────────────────────

/// <summary>Request body for creating a contact.</summary>
public sealed record CreateContactRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Organisation);

/// <summary>Request body for updating a contact.</summary>
public sealed record UpdateContactRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Organisation);
