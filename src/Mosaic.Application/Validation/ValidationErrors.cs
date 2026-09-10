using Mosaic.Contracts;

namespace Mosaic.Application.Validation;

// Collects every failure in one request so the user fixes the whole form at once, instead of one
// field per round trip.
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<FieldError>> _errors = [];

    // for a validator that wants to stop after a field is already known bad
    public bool Any => _errors.Count > 0;

    public void Add(string field, string code, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (!_errors.TryGetValue(field, out var forField))
        {
            forField = [];
            _errors[field] = forField;
        }

        forField.Add(new FieldError { Code = code, Params = parameters });
    }

    public void ThrowIfAny()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        var byField = new Dictionary<string, IReadOnlyList<FieldError>>(_errors.Count);

        foreach (var (field, errors) in _errors)
        {
            byField[field] = errors;
        }

        throw new ValidationException(byField);
    }
}