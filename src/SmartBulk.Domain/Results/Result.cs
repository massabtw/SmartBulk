namespace SmartBulk.Domain.Results;

public class Result
{
    public bool Success { get; }
    public string? Message { get; }
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool success, string? message, IEnumerable<string>? errors = null)
    {
        Success = success;
        Message = message;
        Errors = errors?.ToList() ?? new List<string>();
    }

    public static Result Ok(string? message = null) => new(true, message);
    public static Result Fail(string error) => new(false, error, new[] { error });
    public static Result Fail(IEnumerable<string> errors) => new(false, null, errors);

    public static implicit operator Result(string error) => Fail(error);
}

public class Result<T> : Result
{
    public T? Data { get; }

    protected internal Result(T? data, bool success, string? message, IEnumerable<string>? errors = null)
        : base(success, message, errors)
    {
        Data = data;
    }

    public static Result<T> Ok(T data, string? message = null) => new(data, true, message);
    public static new Result<T> Fail(string error) => new(default, false, error, new[] { error });
    public static new Result<T> Fail(IEnumerable<string> errors) => new(default, false, null, errors);

    public static implicit operator Result<T>(T data) => Ok(data);
    public static implicit operator Result<T>(string error) => Fail(error);
}

