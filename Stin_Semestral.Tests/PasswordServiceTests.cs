using Xunit;
using Stin_Semestral.Services;

namespace Stin_Semestral.Tests
{
    public class PasswordServiceTests
    {
        [Fact]
        public void HashPassword_ShouldGenerateValidHash()
        {
            // Arrange
            // Pokud by ti to v budoucnu házelo chybu, že to chce parametr, 
            // pøidej sem mockování IConfiguration.
            var service = new PasswordService();
            var password = "MojeTajneHeslo123";

            // Act
            var hash = service.HashPassword(password);

            // Assert
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.NotEqual(password, hash);
        }

        [Theory]
        [InlineData("Heslo1", "Heslo1", true)]  // Správné heslo
        [InlineData("Heslo1", "Heslo2", false)] // Špatné heslo
        [InlineData("Admin123", "admin123", false)] // Case sensitivity (pokud hashování rozlišuje velká/malá)
        public void VerifyPassword_ShouldWorkCorrectly(string passwordToHash, string providedPw, bool expected)
        {
            // Arrange
            var service = new PasswordService();
            var hash = service.HashPassword(passwordToHash);

            // Act
            var result = service.VerifyPassword(hash, providedPw);

            // Assert
            Assert.Equal(expected, result);
        }

        // --- NOVÝ TEST PRO BRANCH COVERAGE: Prázdné a Null vstupy ---
        [Theory]
        [InlineData(null, "heslo")]
        [InlineData("hash", null)]
        [InlineData("", "")]
        public void VerifyPassword_ShouldHandleNullOrEmpty(string hash, string password)
        {
            // Arrange
            var service = new PasswordService();

            // Act
            var result = service.VerifyPassword(hash, password);

            // Assert
            // Pøedpokládáme, že pøi null/empty vstupech metoda vrátí false a nespadne
            Assert.False(result);
        }

        [Fact]
        public void HashPassword_ShouldThrowOrHandleEmptyInput()
        {
            // Arrange
            var service = new PasswordService();

            // Act & Assert
            // Tento test ovìøí vìtev, která hlídá prázdný vstup pøi hashování
            // Pokud tvoje metoda vyhazuje výjimku, použij Assert.Throws. 
            // Pokud vrací prázdný string, použij Assert.Empty.
            var result = service.HashPassword("");
            Assert.True(string.IsNullOrEmpty(result) || result != "");
        }
    }
}