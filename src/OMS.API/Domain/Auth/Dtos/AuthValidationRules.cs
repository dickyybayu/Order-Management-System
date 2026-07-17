namespace OMS.API.Domain.Auth.Dtos;

public static class AuthValidationRules
{
    public const int FullNameMaximumLength = 150;
    public const int PasswordMinimumLength = 8;
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$";
    public const string PasswordErrorMessage =
        "Password must be at least 8 characters and include uppercase, lowercase, number, and special characters.";
}
