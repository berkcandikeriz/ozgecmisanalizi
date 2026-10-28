namespace OzgecmisAnalizi.Entities.Dtos;

public record RegisterRequest(string FullName, string Email, string Password, string? City);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(Guid Id, string FullName, string Email, string Role, string? City);
