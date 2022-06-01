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
        return new() {ResultStatus = Status.Success, Value = value};
    }
    
    public static Result<T> Failure(string error)
    {
        return new() {ResultStatus = Status.Failure, Error = error};
    }
    
    public static Result<T> Unauthorized()
    {
        return new() {ResultStatus = Status.Unauthorized};
    }
    
    public static Result<T> ValidationError(string error)
    {
        return new() {ResultStatus = Status.ValidationError, Error = error};
    }
}