using FluentValidation;
using FluentValidation.Results;

namespace StandFast.Ui.Common;

/// <summary>
/// Bridges a FluentValidation validator into MudBlazor's per-field validation callback, so the forms enforce exactly the rules the Application layer
/// enforces on save instead of a second, drifting copy of them.
/// </summary>
public static class FluentValidationAdapter
{
    public static Func<object, string, Task<IEnumerable<string>>> ForField<T>(IValidator<T> validator) => async (model, propertyName) =>
    {
        ValidationResult result = await validator.ValidateAsync(ValidationContext<T>.CreateWithOptions((T)model, options => options.IncludeProperties(propertyName)));
        return result.IsValid ? [] : result.Errors.Select(failure => failure.ErrorMessage);
    };
}
