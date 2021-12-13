using AutoFixture;
using FluentAssertions;
using Hackney.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Controllers;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Controllers
{
    [Collection("LogCall collection")]
    public class ProcessesApiControllerTests
    {
        private ProcessesApiController _classUnderTest;
        private Mock<IGetByIdUseCase> _mockGetByIdUseCase;
        private Mock<ISoleToJointUseCase> _mockSoleToJointUseCase;

        private readonly Mock<IHttpContextWrapper> _mockContextWrapper;
        private readonly Mock<HttpRequest> _mockHttpRequest;
        private readonly HeaderDictionary _requestHeaders;
        private readonly Mock<HttpResponse> _mockHttpResponse;
        private readonly HeaderDictionary _responseHeaders;
        private readonly Fixture _fixture = new Fixture();
        private const string RequestBodyText = "Some request body text";
        public ProcessesApiControllerTests()
        {
            _mockGetByIdUseCase = new Mock<IGetByIdUseCase>();
            _mockSoleToJointUseCase = new Mock<ISoleToJointUseCase>();

            _mockContextWrapper = new Mock<IHttpContextWrapper>();
            _mockHttpRequest = new Mock<HttpRequest>();
            _mockHttpResponse = new Mock<HttpResponse>();

            _classUnderTest = new ProcessesApiController(_mockGetByIdUseCase.Object, _mockSoleToJointUseCase.Object, _mockContextWrapper.Object);

            // changes to allow reading of raw request body
            _mockHttpRequest.SetupGet(x => x.Body).Returns(new MemoryStream(Encoding.Default.GetBytes(RequestBodyText)));

            _requestHeaders = new HeaderDictionary();
            _mockHttpRequest.SetupGet(x => x.Headers).Returns(_requestHeaders);

            _mockContextWrapper
                .Setup(x => x.GetContextRequestHeaders(It.IsAny<HttpContext>()))
                .Returns(_requestHeaders);

            _responseHeaders = new HeaderDictionary();
            _mockHttpResponse.SetupGet(x => x.Headers).Returns(_responseHeaders);

            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.SetupGet(x => x.Request).Returns(_mockHttpRequest.Object);
            mockHttpContext.SetupGet(x => x.Response).Returns(_mockHttpResponse.Object);

            var controllerContext = new ControllerContext(new ActionContext(mockHttpContext.Object, new RouteData(), new ControllerActionDescriptor()));
            _classUnderTest.ControllerContext = controllerContext;
        }

        private static ProcessesQuery ConstructQuery(Guid id)
        {
            return new ProcessesQuery
            {
                Id = id
            };
        }

        private CreateProcess ConstructPostRequest()
        {
            return _fixture.Build<CreateProcess>()
                           .With(x => x.FormData, new JsonElement())
                           .Create();
        }

        private (Process, UpdateProcessQuery, UpdateProcessQueryObject) ConstructPatchRequest()
        {
            var queryObject = _fixture.Build<UpdateProcessQueryObject>()
                                      .With(x => x.FormData, new JsonElement())
                                      .Create();
            var processName = ProcessNamesConstants.SoleToJoint;
            var processResponse = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), null, processName, null);
            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.ProcessName, processResponse.ProcessName)
                                .With(x => x.Id, processResponse.Id)
                                .Create();
            return (processResponse, query, queryObject);
        }

        [Fact]
        public async Task GetProcessWithValidIDReturnsOKResponse()
        {
            var stubHttpContext = new DefaultHttpContext();
            var controllerContext = new ControllerContext(new ActionContext(stubHttpContext, new RouteData(), new ControllerActionDescriptor()));
            _classUnderTest.ControllerContext = controllerContext;

            var process = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), new List<Guid>(), null, null);
            var query = ConstructQuery(process.Id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync(process);

            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as OkObjectResult;

            response.Should().NotBeNull();
            response.StatusCode.Should().Be(200);
            response.Value.Should().BeEquivalentTo(process.ToResponse());

            var expectedEtagValue = $"\"{process.VersionNumber}\"";
            _classUnderTest.HttpContext.Response.Headers.TryGetValue(HeaderConstants.ETag, out StringValues val).Should().BeTrue();
            val.First().Should().Be(expectedEtagValue);
        }

        [Fact]
        public async Task GetProcessWithNonExistentIDReturnsNotFoundResponse()
        {
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync((Process) null);
            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as NotFoundObjectResult;
            response.StatusCode.Should().Be(404);
            response.Value.Should().Be(query.Id);
        }

        [Fact]
        public void GetProcessByIdExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            var exception = new ApplicationException("Test exception");
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ThrowsAsync(exception);

            Func<Task<IActionResult>> func = async () => await _classUnderTest.GetProcessById(query).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        [Fact]
        public async void CreateNewProcessReturnsCreatedResponse()
        {
            // Arrange
            var request = ConstructPostRequest();
            var processName = ProcessNamesConstants.SoleToJoint;
            var processResponse = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, request.TargetId, request.RelatedEntities, processName, null);

            _mockSoleToJointUseCase.Setup(x => x.Execute(It.IsAny<Guid>(), SoleToJointTriggers.StartApplication,
             request.TargetId, request.RelatedEntities, request.FormData, request.Documents, processName))
             .ReturnsAsync(processResponse);

            // Act
            var response = await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);

            // Assert
            response.Should().BeOfType(typeof(CreatedResult));
            (response as CreatedResult).Value.Should().Be(processResponse);
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var request = _fixture.Build<CreateProcess>()
                                  .With(x => x.FormData, new JsonElement())
                                  .Create();
            var processName = ProcessNamesConstants.SoleToJoint;
            var exception = new ApplicationException("Test exception");
            _mockSoleToJointUseCase.Setup(x => x.Execute(It.IsAny<Guid>(), SoleToJointTriggers.StartApplication,
              request.TargetId, request.RelatedEntities, request.FormData, request.Documents, processName)).ThrowsAsync(exception);

            Func<Task<IActionResult>> func = async () => await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        [Fact]
        public async void UpdateProcessSuccessfullyReturnsUpdatedStatus()
        {
            // Arrange
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            _mockSoleToJointUseCase.Setup(x => x.Execute(request.Id, request.ProcessTrigger,
              null, null, requestObject.FormData, requestObject.Documents, request.ProcessName))
                .ReturnsAsync(processResponse);
            // Act
            var response = await _classUnderTest.UpdateProcess(requestObject, request).ConfigureAwait(false);
            // Assert
            response.Should().BeOfType(typeof(NoContentResult));
        }

        [Fact]
        public async void UpdateProcessReturnsNotFoundWhenProcessDoesNotExist()
        {
            // Arrange
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            _mockSoleToJointUseCase.Setup(x => x.Execute(request.Id, SoleToJointTriggers.StartApplication,
              null, null, requestObject.FormData, requestObject.Documents, request.ProcessName))
                .ReturnsAsync((Process) null);
            // Act
            var response = await _classUnderTest.UpdateProcess(requestObject, request).ConfigureAwait(false);
            // Assert
            response.Should().BeOfType(typeof(NotFoundObjectResult));
            (response as NotFoundObjectResult).Value.Should().Be(request.Id);
        }

        [Fact]
        public void UpdateProcessExceptionIsThrown()
        {
            // Arrange
            var exception = new ApplicationException("Test exception");
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            _mockSoleToJointUseCase.Setup(x => x.Execute(request.Id, request.ProcessTrigger,
              null, null, requestObject.FormData, requestObject.Documents, request.ProcessName))
                .ThrowsAsync(exception);
            // Act
            Func<Task<IActionResult>> func = async () => await _classUnderTest.UpdateProcess(requestObject, request).ConfigureAwait(false);
            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
