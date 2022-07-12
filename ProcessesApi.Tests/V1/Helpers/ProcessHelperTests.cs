using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    public class ProcessHelperTests
    {
        private Fixture _fixture;

        public ProcessHelperTests()
        {
            _fixture = new Fixture();
        }

        [Fact]
        public void ValidateKeysThrowsErrorIfFormDataDoesNotContainRequiredValues()
        {
            // Arrange
            var expectedFormDataKeys = new List<string> { "some-form-data", "some-other-form-data" };
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => ProcessHelper.ValidateKeys(requestFormData, expectedFormDataKeys);
            // Assert
            action.Should().Throw<FormDataNotFoundException>()
                  .WithMessage($"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({String.Join(", ", expectedFormDataKeys)}).");
        }

        [Fact]
        public void ValidateKeysDoesNotThrowErrorIfFormDataContainsAllRequiredValues()
        {
            // Arrange
            var expectedFormDataKeys = new List<string> { "some-form-data", "some-other-form-data" };
            var requestFormData = new Dictionary<string, object>
            {
                { expectedFormDataKeys[0], true },
                { expectedFormDataKeys[1], true },
            };
            // Act
            Action action = () => ProcessHelper.ValidateKeys(requestFormData, expectedFormDataKeys);
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }

        [Fact]
        public void ValidateOptionalKeysThrowsErrorIfFormDataDoesNotContainAtLeastOneOfRequiredValues()
        {
            // Arrange
            var expectedFormDataKeys = new List<string> { "some-form-data", "some-other-form-data" };
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => ProcessHelper.ValidateOptionalKeys(requestFormData, expectedFormDataKeys);
            // Assert
            action.Should()
                .Throw<FormDataNotFoundException>()
                .WithMessage($"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({String.Join(", ", expectedFormDataKeys)}).");
        }

        [Fact]
        public void ValidateOptionalKeysDoesNotThrowErrorIfFormDataContainsAtLeaseOneOfRequiredValues()
        {
            // Arrange
            var expectedFormDataKeys = new List<string> { "some-form-data", "some-other-form-data" };
            var requestFormData = new Dictionary<string, object> { { expectedFormDataKeys[0], true } };
            // Act
            Action action = () => ProcessHelper.ValidateOptionalKeys(requestFormData, expectedFormDataKeys);
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }

        [Fact]
        public void ValidateHasNotifiedResidentsThrowsErrorIfMissingValues()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();
            // Act
            Action action = () => ProcessHelper.ValidateHasNotifiedResident(processRequest);
            // Assert
            action.Should()
                  .Throw<FormDataNotFoundException>()
                  .WithMessage($"The request's FormData is invalid: The form data keys supplied ({String.Join(", ", processRequest.FormData.Keys)}) do not include the expected values ({SharedKeys.HasNotifiedResident}).");
        }

        [Fact]
        public void ValidateHasNotifiedResidentThrowsErrorIfHasNotifiedResidentIsNotABool()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();
            processRequest.FormData.Add(SharedKeys.HasNotifiedResident, "not-a-bool");

            // Act
            Action action = () => ProcessHelper.ValidateHasNotifiedResident(processRequest);
            // Assert
            action.Should().Throw<FormDataFormatException>();
        }

        [Fact]
        public void ValidateHasNotifiedResidentDoesNotThrowErrorIfExpectedValuesArePresentAndCorrectFormat()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();
            processRequest.FormData.Add(SharedKeys.HasNotifiedResident, true);
            processRequest.FormData.Add(SharedKeys.Reason, true);

            // Act
            Action action = () => ProcessHelper.ValidateHasNotifiedResident(processRequest);
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }
    }
}
