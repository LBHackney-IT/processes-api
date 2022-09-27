using AutoFixture;
using FluentAssertions;
using Hackney.Core.Http;
using Hackney.Core.JWT;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProcessesApi.V2.Controllers;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V2.UseCase.Interfaces;
using System;
using System.Threading.Tasks;
using Xunit;
using Hackney.Shared.Processes.Boundary.Request.V2;

namespace ProcessesApi.Tests.V2.Controllers
{
    [Collection("LogCall collection")]
    public class ProcessesApiControllerTests
    {
        private ProcessesApiController _classUnderTest;
        private Mock<ICreateProcessUseCase> _mockCreateProcessUseCase;
        private readonly Mock<ITokenFactory> _mockTokenFactory;
        private readonly Mock<IHttpContextWrapper> _mockContextWrapper;
        private readonly Fixture _fixture = new Fixture();

        public ProcessesApiControllerTests()
        {
            _mockCreateProcessUseCase = new Mock<ICreateProcessUseCase>();
            _mockTokenFactory = new Mock<ITokenFactory>();
            _mockContextWrapper = new Mock<IHttpContextWrapper>();

            _classUnderTest = new ProcessesApiController(_mockCreateProcessUseCase.Object,
                                                         _mockContextWrapper.Object,
                                                         _mockTokenFactory.Object);
        }

        [Fact]
        public async void CreateNewProcessReturnsCreatedResponse()
        {
            // Arrange
            var request = _fixture.Create<CreateProcess>();
            var processName = ProcessName.soletojoint;
            var processResponse = _fixture.Create<Process>();

            _mockCreateProcessUseCase.Setup(x => x.Execute(request, processName, It.IsAny<Token>())).ReturnsAsync(processResponse);

            // Act
            var response = await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);

            // Assert
            response.Should().BeOfType(typeof(CreatedResult));
            (response as CreatedResult).Value.Should().Be(processResponse);
        }

        [Theory]
        [InlineData(typeof(FormDataNotFoundException))]
        [InlineData(typeof(FormDataFormatException))]
        [InlineData(typeof(InvalidTriggerException))]
        public async Task CreateNewProcessReturnsBadRequest(Type exceptionType)
        {
            var request = _fixture.Create<CreateProcess>();
            var processName = ProcessName.soletojoint;
            var exception = Activator.CreateInstance(exceptionType) as Exception;

            _mockCreateProcessUseCase.Setup(x => x.Execute(request, processName, It.IsAny<Token>())).ThrowsAsync(exception);

            var response = await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);

            response.Should().BeOfType(typeof(BadRequestObjectResult));
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var request = _fixture.Create<CreateProcess>();
            var processName = ProcessName.soletojoint;
            var exception = new ApplicationException("Test exception");
            _mockCreateProcessUseCase.Setup(x => x.Execute(request, processName, It.IsAny<Token>())).ThrowsAsync(exception);

            Func<Task<IActionResult>> func = async () => await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
