namespace DInventory.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; protected set; }
    public string? Error { get; protected set; }
    public List<string> Errors { get; protected set; } = new();

    public static Result Success() => new() { Succeeded = true };
    public static Result Failure(string error) => new() { Succeeded = false, Error = error, Errors = new List<string> { error } };
    public static Result Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new Result { Succeeded = false, Error = list.FirstOrDefault(), Errors = list };
    }
}

public class Result<T> : Result
{
    public T? Data { get; private set; }

    public static Result<T> Success(T data) => new() { Succeeded = true, Data = data };
    public new static Result<T> Failure(string error) => new() { Succeeded = false, Error = error, Errors = new List<string> { error } };
    public new static Result<T> Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new Result<T> { Succeeded = false, Error = list.FirstOrDefault(), Errors = list };
    }
}
