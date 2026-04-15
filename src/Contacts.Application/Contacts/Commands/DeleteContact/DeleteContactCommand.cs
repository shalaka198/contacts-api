using MediatR;
using Contacts.Application.Common;

namespace Contacts.Application.Contacts.Commands.DeleteContact;

public sealed record DeleteContactCommand(Guid Id) : IRequest<Result>;
