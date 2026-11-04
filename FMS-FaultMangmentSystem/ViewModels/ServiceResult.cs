namespace IncidentManagementSystem.ViewModels;

public sealed class ServiceResult<T>
{
    private static readonly IReadOnlyDictionary<string, string[]> NoValidationErrors =
        new Dictionary<string, string[]>();

    private ServiceResult(
        bool succeeded,
        T? data,
        string? errorMessage,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Succeeded = succeeded;
        Data = data;
        ErrorMessage = errorMessage;
        ValidationErrors = validationErrors;
    }

    public bool Succeeded { get; }
    public T? Data { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static ServiceResult<T> Success(T data) =>
        new(true, data, null, NoValidationErrors);

    public static ServiceResult<T> Failure(string errorMessage) =>
        new(false, default, errorMessage, NoValidationErrors);

    public static ServiceResult<T> ValidationFailure(string field, string errorMessage) =>
        new(false, default, null, new Dictionary<string, string[]>
        {
            [field] = [errorMessage]
        });

    public static ServiceResult<T> ValidationFailure(IReadOnlyDictionary<string, string[]> validationErrors) =>
        new(false, default, null, validationErrors);
}
