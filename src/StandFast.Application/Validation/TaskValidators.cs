using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Common;

namespace StandFast.Application.Validation;

public sealed class TaskStateEditDtoValidator : AbstractValidator<TaskStateEditDto>
{
    public TaskStateEditDtoValidator()
    {
        RuleFor(state => state.Title).NotEmpty().MaximumLength(DomainLimits.TaskStateTitleMaxLength);
        RuleFor(state => state.Group).IsInEnum();
    }
}

public sealed class WorkTaskEditDtoValidator : AbstractValidator<WorkTaskEditDto>
{
    public WorkTaskEditDtoValidator()
    {
        RuleFor(task => task.Title).NotEmpty();
        RuleFor(task => task.Title).Must((task, _) => task.Titles().All(title => title.Length <= DomainLimits.TaskTitleMaxLength)).WithMessage(ValidationMessages.TaskTitleTooLong);
        RuleFor(task => task.Title).Must((task, _) => task.Titles().Count <= 1).WithMessage(ValidationMessages.TaskTitleSingle).When(task => task.Id is not null);
        RuleFor(task => task.StateId).NotNull().NotEqual(Guid.Empty).WithMessage(ValidationMessages.TaskStateRequired);
        RuleFor(task => task.Priority).NotNull().InclusiveBetween(DomainLimits.TaskPriorityHighest, DomainLimits.TaskPriorityLowest).WithMessage(ValidationMessages.TaskPriorityRange);
        RuleFor(task => task.RequestedByPersonId).NotNull().NotEqual(Guid.Empty).WithMessage(ValidationMessages.TaskRequesterRequired);
    }
}
