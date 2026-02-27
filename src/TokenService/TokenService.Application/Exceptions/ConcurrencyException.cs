namespace TokenService.Application.Exceptions;

public class ConcurrencyException(string message, Exception? inner = null)
    : Exception(message, inner);
