using FluentValidation;
using NpiManagement.Application.Npis.Commands;

namespace NpiManagement.Application.Npis.Validation;

public sealed class CreateNpiDraftCommandValidator : AbstractValidator<CreateNpiDraftCommand>
{
    public CreateNpiDraftCommandValidator()
    {
        RuleFor(x => x.Capture).SetValidator(new NpiCaptureDtoValidator());
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class UpdateNpiDraftCommandValidator : AbstractValidator<UpdateNpiDraftCommand>
{
    public UpdateNpiDraftCommandValidator()
    {
        RuleFor(x => x.Capture).SetValidator(new NpiCaptureDtoValidator());
        RuleFor(x => x.Priority).IsInEnum();
    }
}
