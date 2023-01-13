namespace Application.Core;

public class Result<T>
{
    public enum Status
    {
        Success,
        Failure,
        Unauthorized,
        ValidationError,
        NotFound
    }

    public Status ResultStatus { get; set; }
    public string Error { get; set; } = null!;
    public T? Value { get; private init; }

    public static Result<T> Success(T? value)
    {
        return new Result<T> { ResultStatus = Status.Success, Value = value };
    }

    public static Result<T> Failure(string error)
    {
        return new Result<T> { ResultStatus = Status.Failure, Error = error };
    }

    public static Result<T> Unauthorized()
    {
        return new Result<T> { ResultStatus = Status.Unauthorized };
    }

    public static Result<T> ValidationError(string error)
    {
        return new Result<T> { ResultStatus = Status.ValidationError, Error = error };
    }
}