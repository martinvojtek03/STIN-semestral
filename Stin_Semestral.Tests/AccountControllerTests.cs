using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Stin_Semestral.Controllers;
using Stin_Semestral.Services;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Stin_Semestral.Tests
{
    public class AccountControllerTests
    {
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<PasswordService> _mockPasswordService;
        private readonly AccountController _controller;

        public AccountControllerTests()
        {
            _mockConfig = new Mock<IConfiguration>();
            _mockPasswordService = new Mock<PasswordService>();

            _controller = new AccountController(_mockConfig.Object, _mockPasswordService.Object);

            // 1. Mock Authentication Service
            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(x => x.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);
            authServiceMock
                .Setup(x => x.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            // 2. Mock TempData (řeší chybu ITempDataDictionaryFactory)
            var tempDataMock = new Mock<ITempDataDictionary>();
            _controller.TempData = tempDataMock.Object;

            // 3. Mock URL Helper (řeší chybu IUrlHelperFactory)
            var urlHelperMock = new Mock<IUrlHelper>();
            _controller.Url = urlHelperMock.Object;

            // 4. Sestavení HttpContextu se službami
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(s => s.GetService(typeof(IAuthenticationService)))
                .Returns(authServiceMock.Object);

            // Přidáme i prázdný IUrlHelperFactory, kdyby si ho vnitřně žádal
            serviceProviderMock
                .Setup(s => s.GetService(typeof(IUrlHelperFactory)))
                .Returns(new Mock<IUrlHelperFactory>().Object);

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = serviceProviderMock.Object;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task Login_WithCorrectCredentials_ReturnsRedirect()
        {
            // Arrange
            var password = "correct_password";
            var fakeHash = "hashed_value";

            _mockConfig.Setup(c => c["AdminSettings:PasswordHash"]).Returns(fakeHash);
            _mockPasswordService.Setup(p => p.VerifyPassword(fakeHash, password)).Returns(true);

            // Act
            var result = await _controller.Login("admin", password);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsViewWithError()
        {
            // Arrange
            _mockConfig.Setup(c => c["AdminSettings:PasswordHash"]).Returns("some_hash");
            _mockPasswordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            // Act
            var result = await _controller.Login("admin", "wrong_password");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Neplatné přihlašovací údaje.", _controller.ViewBag.Error);
        }

        [Fact]
        public async Task Logout_RedirectsToHome()
        {
            // Act
            var result = await _controller.Logout();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
        }
    }
}