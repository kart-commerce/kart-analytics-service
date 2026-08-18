using FluentValidation;

namespace Kart.Analytics.Application.Features.IngestEvent;

public sealed class IngestEventCommandValidator : AbstractValidator<IngestEventCommand>
{
    public IngestEventCommandValidator()
    {
        RuleFor(x => x.EventId).NotEqual(Guid.Empty);
        RuleFor(x => x.EventType).NotEmpty();
        RuleFor(x => x.PublisherService).NotEmpty();
        RuleFor(x => x.PartitionKey).NotEmpty();
        RuleFor(x => x.PayloadJson).NotEmpty();
    }
}
