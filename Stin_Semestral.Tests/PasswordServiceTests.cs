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
            var service = new PasswordService();
            var password = "MojeTajneHeslo123";

            // Act
            var hash = service.HashPassword(password);

            // Assert
            Assert.NotNull(hash);
            Assert.NotEqual(password, hash); // Hash nesmí být stejný jako heslo
        }

        [Theory]
        [InlineData("Heslo1", "Heslo1", true)]  // Správné heslo
        [InlineData("Heslo1", "Heslo2", false)] // Špatné heslo
        public void VerifyPassword_ShouldWorkCorrectly(string hashedPw, string providedPw, bool expected)
        {
            // Arrange
            var service = new PasswordService();
            var hash = service.HashPassword(hashedPw);

            // Act
            var result = service.VerifyPassword(hash, providedPw);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}