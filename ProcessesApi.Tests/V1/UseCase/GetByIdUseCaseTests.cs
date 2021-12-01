using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;
using Moq;
using FluentAssertions;
using AutoFixture;
using System.Threading.Tasks;
using System;
using Xunit;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class GetByIdUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private GetProcessByIdUseCase _classUnderTest;
        private readonly Fixture _fixture = new Fixture();
        public GetByIdUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _classUnderTest = new GetProcessByIdUseCase(_mockGateway.Object);
        }

        private static ProcessesQuery ConstructQuery(Guid id)
        {
            return new ProcessesQuery
            {
                Id = id
            };
        }

        [Fact]
        public async Task GetProcessByIdReturnsNullIfNullResponseFromGateway()
        {
            var id = Guid.NewGuid();
            _mockGateway.Setup(x => x.GetProcessById(id)).ReturnsAsync((SoleToJointProcess) null);
            var query = ConstructQuery(id);

            var response = await _classUnderTest.Execute(query).ConfigureAwait(false);
            response.Should().BeNull();
        }

        [Fact]
        public async Task GetProcessByIdReturnsProcessFromGateway()
        {
            var process = _fixture.Create<SoleToJointProcess>();
            var query = ConstructQuery(process.Id);

            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync((SoleToJointProcess) process);

            var response = await _classUnderTest.Execute(query).ConfigureAwait(false);
            response.Should().BeEquivalentTo(process);
        }

        [Fact]
        public void GetProcessByIdExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.GetProcessById(id)).ThrowsAsync(exception);
            var query = ConstructQuery(id);

            Func<Task<SoleToJointProcess>> func = async () => await _classUnderTest.Execute(query).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
