using Microsoft.AspNetCore.Identity;

namespace SAPS.Web.Data;

/// <summary>
/// Mensajes de ASP.NET Identity en español (por defecto salen en inglés, por ejemplo
/// "Passwords must be at least 6 characters."). Se registra con AddErrorDescriber en Program.cs.
/// </summary>
public class IdentityErrorDescriberEs : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Ocurrió un error inesperado.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Otro usuario modificó este registro. Recargue la página e inténtelo de nuevo.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "La contraseña actual no es correcta.");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "El código de verificación no es válido.");

    public override IdentityError LoginAlreadyAssociated() =>
        Error(nameof(LoginAlreadyAssociated), "Ya existe un usuario con este inicio de sesión.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), $"El nombre de usuario «{userName}» no es válido. Solo puede contener letras, números y los símbolos permitidos.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), $"El correo «{email}» no es válido.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), $"Ya existe una cuenta con el correo «{userName}».");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), $"Ya existe una cuenta con el correo «{email}».");

    public override IdentityError InvalidRoleName(string? role) =>
        Error(nameof(InvalidRoleName), $"El rol «{role}» no es válido.");

    public override IdentityError DuplicateRoleName(string role) =>
        Error(nameof(DuplicateRoleName), $"El rol «{role}» ya existe.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "Esta cuenta ya tiene una contraseña.");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "El bloqueo de cuenta no está habilitado para este usuario.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Error(nameof(UserAlreadyInRole), $"La cuenta ya tiene el rol «{role}».");

    public override IdentityError UserNotInRole(string role) =>
        Error(nameof(UserNotInRole), $"La cuenta no tiene el rol «{role}».");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"La contraseña debe usar al menos {uniqueChars} caracteres distintos.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe tener al menos un símbolo (por ejemplo ! # $ %).");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "La contraseña debe tener al menos un número (0-9).");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "La contraseña debe tener al menos una letra minúscula (a-z).");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "La contraseña debe tener al menos una letra mayúscula (A-Z).");

    private static IdentityError Error(string codigo, string descripcion) =>
        new() { Code = codigo, Description = descripcion };
}
