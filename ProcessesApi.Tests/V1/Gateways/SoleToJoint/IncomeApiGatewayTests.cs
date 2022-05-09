using AutoFixture;
using FluentAssertions;
using Hackney.Core.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Gateways
{

    [Collection("AppTest collection")]
    public class IncomeApiGatewayTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private IncomeApiGateway _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<IncomeApiGateway>> _logger;
        private readonly Mock<IApiGateway> _mockApiGateway;

        private const string IncomeApiRoute = "https://some-domain.com/api";
        private const string IncomeApiToken = "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";
        private const string ApiName = "Income";
        private const string IncomeApiUrlKey = "IncomeApiUrl";
        private const string IncomeApiTokenKey = "IncomeApiToken";
        private static string paymentAgreementRoute => $"{IncomeApiRoute}/agreements";
        private static string tenanciesRoute => $"{IncomeApiRoute}/tenancies";

        public IncomeApiGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _logger = new Mock<ILogger<IncomeApiGateway>>();

            _mockApiGateway = new Mock<IApiGateway>();
            _mockApiGateway.SetupGet(x => x.ApiName).Returns(ApiName);
            _mockApiGateway.SetupGet(x => x.ApiRoute).Returns(IncomeApiRoute);
            _mockApiGateway.SetupGet(x => x.ApiToken).Returns(IncomeApiToken);

            _classUnderTest = new IncomeApiGateway(_logger.Object, _mockApiGateway.Object);
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

        [Fact]
        public void ConstructorTestInitialisesApiGateway()
        {
            _mockApiGateway.Verify(x => x.Initialise(ApiName, IncomeApiUrlKey, IncomeApiTokenKey, null, true),
                                   Times.Once);
        }

        [Fact]
        public async Task GetPaymentAgreementsByTenancyReferenceReturnsPaymentAgreements()
        {
            // Arrange
            var reference = _fixture.Create<String>();
            var paymentAgreements = _fixture.Create<PaymentAgreements>();

            _mockApiGateway.Setup(x => x.GetByIdAsync<PaymentAgreements>($"{paymentAgreementRoute}/{reference}", reference, It.IsAny<Guid>()))
                           .ReturnsAsync(paymentAgreements);
            // Act
            var result = await _classUnderTest.GetPaymentAgreementsByTenancyReference(reference, Guid.NewGuid()).ConfigureAwait(false);
            // Assert
            result.Should().BeEquivalentTo(paymentAgreements);
        }

        [Fact]
        public void GetPaymentAgreementsByTenancyReferenceExceptionThrown()
        {
            // Arrange
            var reference = _fixture.Create<String>();
            var exMessage = "This is an exception";
            _mockApiGateway.Setup(x => x.GetByIdAsync<PaymentAgreements>($"{paymentAgreementRoute}/{reference}", reference, It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception(exMessage));

            // Act + Assert
            _classUnderTest
                .Invoking(cut => cut.GetPaymentAgreementsByTenancyReference(reference, Guid.NewGuid()))
                .Should().Throw<Exception>().WithMessage(exMessage);
        }

        [Fact]
        public async Task GetTenancyByReferenceReturnsNull()
        {
            // Arrange
            var reference = _fixture.Create<String>();
            // Act
            var result = await _classUnderTest.GetTenancyByReference(reference, Guid.NewGuid()).ConfigureAwait(false);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetTenancyByReferenceReturnsTenancyInformation()
        {
            // Arrange
            var reference = _fixture.Create<String>();
            var tenancy = _fixture.Build<Tenancy>().With(x => x.TenancyRef, reference).Create();

            _mockApiGateway.Setup(x => x.GetByIdAsync<Tenancy>($"{tenanciesRoute}/{reference}", reference, It.IsAny<Guid>()))
                           .ReturnsAsync(tenancy);
            // Act
            var result = await _classUnderTest.GetTenancyByReference(reference, Guid.NewGuid()).ConfigureAwait(false);
            // Assert
            result.Should().BeEquivalentTo(tenancy);
        }

        [Fact]
        public void GetTenancyByReferenceExceptionThrown()
        {
            // Arrange
            var reference = _fixture.Create<String>();
            var exMessage = "This is an exception";
            _mockApiGateway.Setup(x => x.GetByIdAsync<Tenancy>($"{tenanciesRoute}/{reference}", reference, It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception(exMessage));

            // Act + Assert
            _classUnderTest
                .Invoking(cut => cut.GetTenancyByReference(reference, Guid.NewGuid()))
                .Should().Throw<Exception>().WithMessage(exMessage);
        }
    }
}
