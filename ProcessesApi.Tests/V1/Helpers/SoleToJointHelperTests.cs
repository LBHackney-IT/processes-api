using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    public class EligibilityFailureTestCase
    {
        public string Name { get; set; }
        public Func<TenureInformation, Guid, TenureInformation> Function { get; set; }

        public override string ToString()
        {
            return Name.ToString();
        }
    }

    [Collection("AppTest collection")]
    public class SoleToJointHelperTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;
        private SoleToJointHelper _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<SoleToJointHelper>> _logger;


        public SoleToJointHelperTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<SoleToJointHelper>>();
            _classUnderTest = new SoleToJointHelper(_dynamoDb, _logger.Object);
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

        private async Task InsertDatatoDynamoDB(TenureInformationDb entity)
        {
            await _dbFixture.SaveEntityAsync(entity).ConfigureAwait(false);
        }

        private (Guid, TenureInformation) CreateEligibleTenure()
        {
            var incomingTenantId = Guid.NewGuid();
            var processTenure = _fixture.Build<TenureInformation>()
                        .With(x => x.HouseholdMembers,
                                new List<HouseholdMembers> {
                                    _fixture.Build<HouseholdMembers>()
                                    .With(x => x.Id, incomingTenantId)
                                    .With(x => x.PersonTenureType, PersonTenureType.Tenant)
                                    .With(x => x.IsResponsible, true)
                                    .With(x => x.DateOfBirth, DateTime.Now.AddYears(-18))
                                    .Create()
                                })
                        .With(x => x.TenureType, TenureTypes.Secure)
                        .With(x => x.EndOfTenureDate, (DateTime?) null)
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            return (incomingTenantId, processTenure);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var incomingTenantId, var processTenure) = CreateEligibleTenure();
            await InsertDatatoDynamoDB(processTenure.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(processTenure.Id, incomingTenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {processTenure.Id}", Times.Once());
        }

        public class EligiiblityFailureTestCases : IEnumerable<object[]>
        {
            private readonly Fixture _fixture = new Fixture();

            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[]
                {
                    new EligibilityFailureTestCase
                    {
                        Name = "Incoming tenant is not a named tenure holder",
                        Function = (Func<TenureInformation, Guid, TenureInformation>) (
                            (TenureInformation processTenure, Guid incomingTenantId) =>
                            {
                                processTenure.HouseholdMembers.ToListOrEmpty()
                                                              .Find(x => x.Id == incomingTenantId)
                                                              .PersonTenureType = PersonTenureType.Occupant;
                                return processTenure;
                            }
                        )
                    }
                };
                yield return new object[]
                {
                    new EligibilityFailureTestCase
                    {
                        Name = "More than one responsible person is on the tenure",
                        Function = (Func<TenureInformation, Guid, TenureInformation>) (
                            (TenureInformation processTenure, Guid incomingTenantId) =>
                            {
                                var householdMembers = processTenure.HouseholdMembers.ToListOrEmpty();
                                householdMembers.Add(_fixture.Build<HouseholdMembers>()
                                                             .With(x => x.IsResponsible, true)
                                                             .Create());
                                processTenure.HouseholdMembers = householdMembers;
                                return processTenure;
                            }
                        )
                    }
                };
                yield return new object[]
                {
                    new EligibilityFailureTestCase
                    {
                        Name = "The tenure is not currently active",
                        Function = (Func<TenureInformation, Guid, TenureInformation>) (
                            (TenureInformation processTenure, Guid incomingTenantId) =>
                            {
                                processTenure.EndOfTenureDate = DateTime.Now.AddDays(-10);
                                return processTenure;
                            }
                        )
                    }
                };
                yield return new object[]
                {
                    new EligibilityFailureTestCase
                    {
                        Name = "The tenure is not secure",
                        Function = (Func<TenureInformation, Guid, TenureInformation>) (
                            (TenureInformation processTenure, Guid incomingTenantId) =>
                            {
                                processTenure.TenureType = TenureTypes.NonSecure;
                                return processTenure;
                            }
                        )
                    }
                };
                yield return new object[]
                {
                    new EligibilityFailureTestCase
                    {
                        Name = "The tenant is a minor",
                        Function = (Func<TenureInformation, Guid, TenureInformation>) (
                            (TenureInformation processTenure, Guid incomingTenantId) =>
                            {
                                processTenure.HouseholdMembers.ToListOrEmpty()
                                                              .Find(x => x.Id == incomingTenantId)
                                                              .DateOfBirth = DateTime.Now;
                                return processTenure;
                            }
                        )
                    }
                };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(EligiiblityFailureTestCases))]
        public async Task CheckEligibiltyReturnsFalseIfEligibilityCriteriaIsNotMet(EligibilityFailureTestCase testCase)
        {
            // Arrange
            (var incomingTenantId, var eligibleTenure) = CreateEligibleTenure();
            var processTenure = testCase.Function.Invoke(eligibleTenure, incomingTenantId);
            await InsertDatatoDynamoDB(processTenure.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(processTenure.Id, incomingTenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {processTenure.Id}", Times.Once());
        }

    }
}
