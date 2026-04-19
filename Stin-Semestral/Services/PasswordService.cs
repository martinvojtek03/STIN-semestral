using Microsoft.AspNetCore.Identity;

namespace Stin_Semestral.Services
{
    public class PasswordService
    {
        // PasswordHasher vyžaduje generický typ pro identitu uživatele, 
        // ale jelikož máme jen jednoho admina, stačí nám string.
        private readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        /// <summary>
        /// Vytvoří bezpečný hash hesla.
        /// </summary>
        public string HashPassword(string password)
        {
            // První parametr je "user instance", v našem případě stačí dummy string "admin"
            return _hasher.HashPassword("admin", password);
        }

        /// <summary>
        /// Ověří, zda zadané heslo odpovídá uloženému hashi.
        /// </summary>
        public virtual bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            var result = _hasher.VerifyHashedPassword("admin", hashedPassword, providedPassword);

            // Success znamená, že heslo sedí. 
            // Existuje i SuccessRehashNeeded, pokud by .NET změnil algoritmus na silnější, 
            // ale pro naše účely stačí Success.
            return result == PasswordVerificationResult.Success;
        }
    }
}