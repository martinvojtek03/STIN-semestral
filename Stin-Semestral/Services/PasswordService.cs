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
            // Ošetření prázdného hesla - dobré pro coverage
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            // První parametr je "user instance", v našem případě stačí dummy string "admin"
            return _hasher.HashPassword("admin", password);
        }

        /// <summary>
        /// Ověří, zda zadané heslo odpovídá uloženému hashi.
        /// </summary>
        public virtual bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            // --- OPRAVA PRO SELHÁVAJÍCÍ TESTY ---
            // PasswordHasher.VerifyHashedPassword vyhazuje ArgumentNullException, pokud je některý parametr null.
            // Ručním ověřením zajistíme, že metoda vrátí false namísto pádu aplikace.
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            {
                return false;
            }

            try
            {
                var result = _hasher.VerifyHashedPassword("admin", hashedPassword, providedPassword);

                // Success znamená, že heslo sedí. 
                return result == PasswordVerificationResult.Success;
            }
            catch (Exception)
            {
                // Pokud by došlo k jiné chybě (např. neplatný formát hashe), vrátíme false
                return false;
            }
        }
    }
}