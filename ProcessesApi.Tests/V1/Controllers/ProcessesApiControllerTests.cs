using AutoFixture;
using ProcessesApi.V1.Controllers;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.UseCase.Interfaces;
using Moq;
using Xunit;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Linq;
using ProcessesApi.V1.Boundary.Response;

namespace ProcessesApi.Tests.V1.Controllers
{
    [Collection("LogCall collection")]
    public class ProcessesApiControllerTests
    {
        private ProcessesApiController _classUnderTest;
        private Mock<IGetByIdUseCase> _mockGetByIdUseCase;
        private Mock<ICreateNewProcessUsecase> _mockCreateProcessUseCase;
        private readonly Fixture _fixture = new Fixture();

        public ProcessesApiControllerTests()
        {
            _mockGetByIdUseCase = new Mock<IGetByIdUseCase>();
            _mockCreateProcessUseCase = new Mock<ICreateNewProcessUsecase>();
            _classUnderTest = new ProcessesApiController(_mockGetByIdUseCase.Object, _mockCreateProcessUseCase.Object);
        }

        private static ProcessesQuery ConstructQuery(Guid Id)
        {
            return new ProcessesQuery
            {
                Id = Id
            };
        }

        private CreateProcessQuery ConstructPostRequest()
        {
            return _fixture.Build<CreateProcessQuery>()
                           .Create();
        }

        [Fact]
        public async Task GetProcessWithValidIDReturnsOKResponse()
        {
            var stubHttpContext = new DefaultHttpContext();
            var controllerContext = new ControllerContext(new ActionContext(stubHttpContext, new RouteData(), new ControllerActionDescriptor()));
            _classUnderTest.ControllerContext = controllerContext;

            var process = _fixture.Create<Process>();
            var query = ConstructQuery(process.Id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync(process);

            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as OkObjectResult;

            response.Should().NotBeNull();
            response.StatusCode.Should().Be(200);
            response.Value.Should().BeEquivalentTo(process.ToResponse());

            var expectedEtagValue = $"\"{process.VersionNumber}\"";
            _classUnderTest.HttpContext.Response.Headers.TryGetValue(HeaderConstants.ETag, out StringValues val).Should().BeTrue();
            val.First().Should().Be(expectedEtagValue);
        }

        [Fact]
        public async Task GetProcessWithNonExistentIDReturnsNotFoundResponse()
        {
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync((Process) null);
            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as NotFoundObjectResult;
            response.StatusCode.Should().Be(404);
            response.Value.Should().Be(query.Id);
        }

        [Fact]
        public void GetProcessByIdExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            var exception = new ApplicationException("Test exception");
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ThrowsAsync(exception);

            Func<Task<IActionResult>> func = async () => await _classUnderTest.GetProcessById(query).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        [Fact]
        public async void CreateNewProcessReturnsCreatedResponse()
        {
            // Arrange
            var processResponse = _fixture.Create<ProcessResponse>();
            var processName = "some-process";
            _mockCreateProcessUseCase.Setup(x => x.Execute(It.IsAny<CreateProcessQuery>(), processName))
                .ReturnsAsync(processResponse);

            // Act
            var request = ConstructPostRequest();
            var response = await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);

            // Assert
            response.Should().BeOfType(typeof(CreatedResult));
            (response as CreatedResult).Value.Should().Be(processResponse);
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var query = _fixture.Create<CreateProcessQuery>();
            var processName = "some-process";
            var exception = new ApplicationException("Test exception");
            _mockCreateProcessUseCase.Setup(x => x.Execute(query, processName)).ThrowsAsync(exception);

            Func<Task<IActionResult>> func = async () => await _classUnderTest.CreateNewProcess(query, processName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
