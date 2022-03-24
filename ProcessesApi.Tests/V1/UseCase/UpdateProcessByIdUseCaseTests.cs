using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.UseCase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class UpdateProcessByIdUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private UpdateProcessByIdUsecase _classUnderTest;
        private readonly Fixture _fixture = new Fixture();

        public UpdateProcessByIdUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _classUnderTest = new UpdateProcessByIdUsecase(_mockGateway.Object);
        }

        private UpdateProcessByIdQuery ConstructQuery(Guid id)
        {
            return new UpdateProcessByIdQuery() { Id = id };
        }

        private UpdateProcessByIdRequestObject ConstructRequest()
        {
            return new UpdateProcessByIdRequestObject();
        }

        private UpdateProcess ConstructProcess()
        {
            return _fixture.Create<UpdateProcess>();
        }



        private UpdateProcess ConstructUpdateRequest()
        {
            var processData = ProcessData.Create(_fixture.Create<Dictionary<string, object>>(), new List<Guid>());
            var assignment = _fixture.Create<Assignment>();
            var request = UpdateProcess.Create(Guid.NewGuid(), processData, assignment);
            return request;
        }


        [Fact]
        public async Task UpdateProcessWhenProcessDoesNotExistReturnsNull()
        {
            var updatedProcess = ConstructProcess();
            var query = ConstructQuery(updatedProcess.Id);
            var request = ConstructRequest();

            _mockGateway.Setup(x => x.SaveProcessById(query, request, It.IsAny<int?>())).ReturnsAsync((Process) null);

            var response = await _classUnderTest.Execute(query,
                                                         request,
                                                         It.IsAny<int?>()).ConfigureAwait(false);

            response.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(3)]
        public async Task UpdateProcessByIdReturnsResult(int? ifMatch)
        {
            var updatedProcess = ConstructProcess();
            var query = ConstructQuery(updatedProcess.Id);
            var request = ConstructRequest();

            var process = _fixture.Build<Process>()
                                  .With(x => x.Id, updatedProcess.Id)
                                  .Create();

            _mockGateway.Setup(x => x.SaveProcessById(query, request, ifMatch)).ReturnsAsync(process);

            var response = await _classUnderTest.Execute(query, request, ifMatch).ConfigureAwait(false);
            response.Id.Should().Be(query.Id);
            response.Should().BeEquivalentTo(process, config => config.Excluding(y => y.Id));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(3)]
        public void UpdateProcessByIdExceptionIsThrown(int? ifMatch)
        {
            var updatedProcess = ConstructProcess();
            var query = ConstructQuery(updatedProcess.Id);
            var request = ConstructRequest();

            var exception = new ApplicationException("Test exception");
            _mockGateway.Setup(x => x.SaveProcessById(query, request, ifMatch)).ThrowsAsync(exception);

            // Act
            Func<Task<Process>> func = async () =>
                await _classUnderTest.Execute(query, request, ifMatch).ConfigureAwait(false);

            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

    }
}
