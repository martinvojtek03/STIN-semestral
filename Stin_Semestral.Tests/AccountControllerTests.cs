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
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

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

            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(x => x.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);
            authServiceMock
                .Setup(x => x.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            var tempDataMock = new Mock<ITempDataDictionary>();
            _controller.TempData = tempDataMock.Object;

            var urlHelperMock = new Mock<IUrlHelper>();
            _controller.Url = urlHelperMock.Object;

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(s => s.GetService(typeof(IAuthenticationService)))
                .Returns(authServiceMock.Object);

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
            _mockConfig.Setup(c => c["AdminSettings:PasswordHash"]).Returns("hashed_value");
            _mockPasswordService.Setup(p => p.VerifyPassword("hashed_value", "correct_password")).Returns(true);

            var result = await _controller.Login("admin", "correct_password");

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
        }

        // --- NOVÝ TEST PRO BRANCH COVERAGE: Špatné jméno ---
        [Fact]
        public async Task Login_WithWrongUsername_ReturnsViewWithError()
        {
            // Act - Posíláme jiné jméno než "admin"
            var result = await _controller.Login("not_admin", "any_password");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Neplatné přihlašovací údaje.", _controller.ViewBag.Error);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsViewWithError()
        {
            _mockConfig.Setup(c => c["AdminSettings:PasswordHash"]).Returns("some_hash");
            _mockPasswordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            var result = await _controller.Login("admin", "wrong_password");

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Neplatné přihlašovací údaje.", _controller.ViewBag.Error);
        }

        // --- NOVÝ TEST PRO BRANCH COVERAGE: Prázdné vstupy ---
        [Theory]
        [InlineData("", "")]
        [InlineData(null, null)]
        public async Task Login_WithEmptyCredentials_ReturnsViewWithError(string user, string pass)
        {
            // Act
            var result = await _controller.Login(user, pass);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Neplatné přihlašovací údaje.", _controller.ViewBag.Error);
        }

        [Fact]
        public async Task Logout_RedirectsToHome()
        {
            var result = await _controller.Logout();

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
        }
    }
}