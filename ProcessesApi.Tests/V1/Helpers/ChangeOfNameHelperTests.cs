using FluentAssertions;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    public class ChangeOfNameHelperTests
    {
        [Fact]
        public void ValidateFormDataThrowsErrorIfFormDataDoesNotContainAtLeastOneOfRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => ChangeOfNameHelper.ValidateOptionalFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().Throw<FormDataNotFoundException>()
                  .WithMessage($"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({expectedFormDataKey}).");
        }

        [Fact]
        public void ValidateFormDataDoesNotThrowErrorIfFormDataContainsAllRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "firstName";
            var requestFormData = new Dictionary<string, object>() { { expectedFormDataKey, true } };
            // Act
            Action action = () => ProcessHelper.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }
    }
}