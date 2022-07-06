using FluentAssertions;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    public class SharedHelperTests
    {
        [Fact]
        public void ValidateFormDataThrowsErrorIfFormDataDoesNotContainRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => SharedHelper.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
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
            Action action = () => SharedHelper.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }
    }
}
