using AutoFixture;
using FluentAssertions;
using Hackney.Core.Http;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Gateways
{

    [Collection("AppTest collection")]
    public class SoleToJointGatewayTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly Fixture _fixture = new Fixture();
        private SoleToJointGateway _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<SoleToJointGateway>> _logger;
        private readonly Mock<IApiGateway> _mockApiGateway;

        private const string IncomeApiRoute = "https://some-domain.com/api";
        private const string IncomeApiToken = "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";
        private const string ApiName = "Income";
        private const string IncomeApiUrlKey = "IncomeApiUrl";
        private const string IncomeApiTokenKey = "IncomeApiToken";
        private static string paymentAgreementRoute => $"{IncomeApiRoute}/agreements";
        private static string tenanciesRoute => $"{IncomeApiRoute}/tenancies";

        public SoleToJointGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;

            _logger = new Mock<ILogger<SoleToJointGateway>>();

            _mockApiGateway = new Mock<IApiGateway>();
            _mockApiGateway.SetupGet(x => x.ApiName).Returns(ApiName);
            _mockApiGateway.SetupGet(x => x.ApiRoute).Returns(IncomeApiRoute);
            _mockApiGateway.SetupGet(x => x.ApiToken).Returns(IncomeApiToken);

            _classUnderTest = new SoleToJointGateway(_dbFixture.DynamoDbContext, _logger.Object, _mockApiGateway.Object);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                foreach (var action in _cleanup)
                    action();

                _disposed = true;
            }
        }

        private void AllTestsShouldHaveRun(TenureInformation tenure, Person proposedTenant, string tenancyRef)
        {
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API for payment agreement with tenancy ref: {tenancyRef}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API with tenancy ref: {tenancyRef}", Times.Once());
        }


        [Fact]
        public void ConstructorTestInitialisesApiGateway()
        {
            _mockApiGateway.Verify(x => x.Initialise(ApiName, IncomeApiUrlKey, IncomeApiTokenKey, null, true),
                                   Times.Once);
        }
    }
}
