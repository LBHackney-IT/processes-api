using AutoFixture;
using ProcessesApi.V1.Controllers;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.UseCase.Interfaces;
using Moq;
using Xunit;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;

namespace ProcessesApi.Tests.V1.Controllers
{
    [Collection("LogCall collection")]
    public class ProcessesApiControllerTests
    {
        private ProcessesApiController _classUnderTest;
        private Mock<IGetByIdUseCase> _mockGetByIdUseCase;
        private readonly Fixture _fixture = new Fixture();

        public ProcessesApiControllerTests()
        {
            _mockGetByIdUseCase = new Mock<IGetByIdUseCase>();
            _classUnderTest = new ProcessesApiController(_mockGetByIdUseCase.Object);
        }

        private static ProcessesQuery ConstructQuery(Guid Id)
        {
            return new ProcessesQuery
            {
                Id = Id
            };
        }

        [Fact]
        public async Task GetProcessWithValidIDReturnsOKResponse()
        {
            var expectedResponse = _fixture.Create<ProcessesResponse>();
            var query = ConstructQuery(expectedResponse.Id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync(expectedResponse);

            var actualResponse = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as OkObjectResult;

            actualResponse.Should().NotBeNull();
            actualResponse.StatusCode.Should().Be(200);
            actualResponse.Value.Should().BeEquivalentTo(expectedResponse);
        }

        [Fact]
        public async Task GetProcessWithNonExistentIDReturnsNotFoundResponse()
        {
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync((ProcessesResponse) null);
            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as NotFoundObjectResult;
            response.StatusCode.Should().Be(404);
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
    }
}
