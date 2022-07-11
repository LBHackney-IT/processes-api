using AutoFixture;
using FluentAssertions;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Moq;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Constants.SoleToJoint;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.UseCase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ProcessesApi.V1.Constants;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class UpdateProcessByIdUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private readonly Mock<ISnsGateway> _processesSnsGateway;
        private readonly Mock<ProcessesSnsFactory> _processesSnsFactory;
        private UpdateProcessByIdUsecase _classUnderTest;
        private readonly Fixture _fixture = new Fixture();

        public UpdateProcessByIdUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _processesSnsGateway = new Mock<ISnsGateway>();
            _processesSnsFactory = new Mock<ProcessesSnsFactory>();
            _classUnderTest = new UpdateProcessByIdUsecase(_mockGateway.Object, _processesSnsGateway.Object, _processesSnsFactory.Object);
        }

        private ProcessQuery ConstructQuery(Guid id)
        {
            return new ProcessQuery() { Id = id };
        }

        private UpdateProcessByIdRequestObject ConstructRequest()
        {
            return _fixture.Create<UpdateProcessByIdRequestObject>();
        }


        [Fact]
        public async Task UpdateProcessWhenProcessDoesNotExistReturnsNull()
        {
            var process = _fixture.Create<Process>();
            var query = ConstructQuery(process.Id);
            var request = ConstructRequest();
            var token = new Token();

            _mockGateway.Setup(x => x.UpdateProcessById(query, request, It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync((UpdateEntityResult<ProcessState>) null);

            var response = await _classUnderTest.Execute(query,
                                                         request,
                                                         "",
                                                         It.IsAny<int?>(),
                                                         token).ConfigureAwait(false);

            response.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(3)]
        public async Task UpdateProcessByIdReturnsResult(int? ifMatch)
        {
            var process = _fixture.Create<Process>();
            var query = ConstructQuery(process.Id);
            var request = ConstructRequest();
            var token = new Token();

            var updateProcess = _fixture.Build<Process>()
                                  .With(x => x.Id, process.Id)
                                  .Create();
            var formData = new Dictionary<string, object>() { { SharedKeys.AppointmentDateTime, DateTime.UtcNow } };
            var gatewayResult = new UpdateEntityResult<ProcessState>()
            {
                UpdatedEntity = updateProcess.CurrentState,
                OldValues = new Dictionary<string, object>
                {
                    {  "documents", Guid.NewGuid().ToString() },
                    {  "formData", formData }
                },
                NewValues = new Dictionary<string, object>
                {
                    {  "documents", Guid.NewGuid().ToString() },
                    {  "formData", formData }
                }
            };
            _mockGateway.Setup(x => x.UpdateProcessById(query, request, It.IsAny<string>(), ifMatch)).ReturnsAsync(gatewayResult);

            var response = await _classUnderTest.Execute(query, request, "", ifMatch, token).ConfigureAwait(false);
            response.Should().BeEquivalentTo(updateProcess.CurrentState);

        }

        [Theory]
        [InlineData(null)]
        [InlineData(3)]
        public void UpdateProcessByIdExceptionIsThrown(int? ifMatch)
        {
            var process = _fixture.Create<Process>();
            var query = ConstructQuery(process.Id);
            var request = ConstructRequest();

            var exception = new ApplicationException("Test exception");
            _mockGateway.Setup(x => x.UpdateProcessById(query, request, It.IsAny<string>(), ifMatch)).ThrowsAsync(exception);

            // Act
            Func<Task<ProcessState>> func = async () =>
                await _classUnderTest.Execute(query, request, "", ifMatch, It.IsAny<Token>()).ConfigureAwait(false);

            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

    }
}
