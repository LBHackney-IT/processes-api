using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.DynamoDb;
using Moq;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class GetByTargetIdUseCaseTests
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly Mock<IProcessesGateway> _mockGateway;
        private readonly GetProcessesByTargetIdUseCase _classUnderTest;

        public GetByTargetIdUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _classUnderTest = new GetProcessesByTargetIdUseCase(_mockGateway.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("some-value")]
        public async Task GetByTargetIdUseCaseGatewayReturnsNullReturnsEmptyList(string paginationToken)
        {
            // Arrange
            var id = Guid.NewGuid();
            var query = new GetProcessesByTargetIdRequest { TargetId = id, PaginationToken = paginationToken };
            var gatewayResult = new PagedResult<Process>(null, new PaginationDetails(paginationToken));
            _mockGateway.Setup(x => x.GetProcessesByTargetId(query)).ReturnsAsync(gatewayResult);

            // Act
            var response = await _classUnderTest.Execute(query).ConfigureAwait(false);

            // Assert
            response.Results.Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("some-value")]
        public async Task GetByTargetIdUseCaseGatewayReturnsListReturnsResponseList(string paginationToken)
        {
            // Arrange
            var id = Guid.NewGuid();
            var query = new GetProcessesByTargetIdRequest { TargetId = id, PaginationToken = paginationToken };
            var processes = _fixture.CreateMany<Process>(5).ToList();
            var gatewayResult = new PagedResult<Process>(processes, new PaginationDetails(paginationToken));
            _mockGateway.Setup(x => x.GetProcessesByTargetId(query)).ReturnsAsync(gatewayResult);

            // Act
            var response = await _classUnderTest.Execute(query).ConfigureAwait(false);

            // Assert
            response.Results.Should().BeEquivalentTo(processes.ToResponse());
            if (string.IsNullOrEmpty(paginationToken))
                response.PaginationDetails.NextToken.Should().BeNull();
            else
                response.PaginationDetails.DecodeNextToken().Should().Be(paginationToken);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("some-value")]
        public async Task GetByTargetIdExceptionIsThrown(string paginationToken)
        {
            // Arrange
            var id = Guid.NewGuid();
            var query = new GetProcessesByTargetIdRequest { TargetId = id, PaginationToken = paginationToken };
            var exception = new ApplicationException("Test exception");
            _mockGateway.Setup(x => x.GetProcessesByTargetId(query)).ThrowsAsync(exception);

            // Act + Assert
            (await _classUnderTest.Invoking(x => x.Execute(query))
                           .Should()
                           .ThrowAsync<ApplicationException>())
                           .WithMessage(exception.Message);
        }
    }
}
