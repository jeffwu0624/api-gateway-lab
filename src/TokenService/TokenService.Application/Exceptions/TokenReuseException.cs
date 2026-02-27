namespace TokenService.Application.Exceptions;

public class TokenReuseException(string message)
    : Exception(message);
