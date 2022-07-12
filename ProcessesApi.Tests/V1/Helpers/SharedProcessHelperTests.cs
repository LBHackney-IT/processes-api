using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.ChangeOfName;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    public class SharedProcessHelperTests
    {
        private Fixture _fixture;

        public SharedProcessHelperTests()
        {
            _fixture = new Fixture();
        }
        [Fact]
        public void ValidateFormDataThrowsErrorIfFormDataDoesNotContainRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => ProcessHelper.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().Throw<FormDataNotFoundException>()
                  .WithMessage($"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({expectedFormDataKey}).");
        }

        [Fact]
        public void ValidateFormDataDoesNotThrowErrorIfFormDataContainsAllRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>() { { expectedFormDataKey, true } };
            // Act
            Action action = () => ProcessHelper.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }

        [Fact]
        public void ValidateFormDataThrowsErrorIfFormDataDoesNotContainAtLeastOneOfRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => ProcessHelper.ValidateOptionalFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().Throw<FormDataNotFoundException>()
                  .WithMessage($"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({expectedFormDataKey}).");
        }

        [Fact]
        public void ValidateFormDataDoesNotThrowErrorIfFormDataContainsAtLeaseOneOfRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = ChangeOfNameKeys.FirstName;
            var requestFormData = new Dictionary<string, object>() { { expectedFormDataKey, true } };
            // Act
            Action action = () => ProcessHelper.ValidateOptionalFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }


        [Fact]
        public void ValidateHasNotifiedResidentDoesNotThrowError()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();
            var notifiedResident = SharedKeys.HasNotifiedResident;
            var reason = SharedKeys.Reason;
            processRequest.FormData.Add(notifiedResident, true);
            processRequest.FormData.Add(reason, true);

            // Act
            Action action = () => ProcessHelper.ValidateHasNotifiedResident(processRequest);
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }

        [Fact]
        public void ValidateHasNotifiedResidentDoesNotThrowErrorWithoutReason()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();
            var notifiedResident = SharedKeys.HasNotifiedResident;
            processRequest.FormData.Add(notifiedResident, true);

            // Act
            Action action = () => ProcessHelper.ValidateHasNotifiedResident(processRequest);
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }

        [Fact]
        public void ValidateHasNotifiedResidentDoesThrowErrorWithoutNotifyResident()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();
            var reason = SharedKeys.Reason;
            processRequest.FormData.Add(reason, true);

            // Act
            Action action = () => ProcessHelper.ValidateHasNotifiedResident(processRequest);
            // Assert
            action.Should().Throw<FormDataNotFoundException>()
                              .WithMessage($"The request's FormData is invalid: The form data keys supplied ({reason}) do not include the expected values ({SharedKeys.HasNotifiedResident}).");
        }
    }
}
