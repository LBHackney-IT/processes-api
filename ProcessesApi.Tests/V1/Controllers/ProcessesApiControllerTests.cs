using AutoFixture;
using FluentAssertions;
using Hackney.Core.DynamoDb;
using Hackney.Core.Http;
using Hackney.Core.JWT;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Controllers;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Controllers
{
    [Collection("LogCall collection")]
    public class ProcessesApiControllerTests
    {
        private ProcessesApiController _classUnderTest;
        private Mock<IGetByIdUseCase> _mockGetByIdUseCase;
        private Mock<ICreateProcessUseCase> _mockCreateProcessUseCase;
        private Mock<IUpdateProcessUseCase> _mockUpdateProcessUseCase;
        private Mock<IUpdateProcessByIdUseCase> _mockUpdateProcessByIdUseCase;
        private Mock<IGetProcessesByTargetIdUseCase> _mockGetProcessByTargetIdUseCase;

        private readonly Mock<ITokenFactory> _mockTokenFactory;
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
            _mockCreateProcessUseCase = new Mock<ICreateProcessUseCase>();
            _mockUpdateProcessUseCase = new Mock<IUpdateProcessUseCase>();
            _mockUpdateProcessByIdUseCase = new Mock<IUpdateProcessByIdUseCase>();
            _mockGetProcessByTargetIdUseCase = new Mock<IGetProcessesByTargetIdUseCase>();


            _mockTokenFactory = new Mock<ITokenFactory>();
            _mockContextWrapper = new Mock<IHttpContextWrapper>();
            _mockHttpRequest = new Mock<HttpRequest>();
            _mockHttpResponse = new Mock<HttpResponse>();

            _classUnderTest = new ProcessesApiController(_mockGetByIdUseCase.Object,
                                                         _mockCreateProcessUseCase.Object,
                                                         _mockUpdateProcessUseCase.Object,
                                                         _mockUpdateProcessByIdUseCase.Object,
                                                         _mockGetProcessByTargetIdUseCase.Object,
                                                         _mockContextWrapper.Object,
                                                         _mockTokenFactory.Object);

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

        private static ProcessQuery ConstructQuery(Guid id)
        {
            return new ProcessQuery
            {
                Id = id
            };
        }


        private (Process, UpdateProcessQuery, UpdateProcessRequestObject) ConstructPatchRequest()
        {
            var processResponse = _fixture.Create<Process>();
            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.ProcessName, processResponse.ProcessName)
                                .With(x => x.Id, processResponse.Id)
                                .Create();
            var queryObject = _fixture.Create<UpdateProcessRequestObject>();
            return (processResponse, query, queryObject);
        }

        private (ProcessState, ProcessQuery, UpdateProcessByIdRequestObject) ConstructPatchByIdRequest()
        {
            var processResponse = _fixture.Create<Process>();
            var currentProcessState = _fixture.Create<ProcessState>();
            processResponse.CurrentState = currentProcessState;

            var query = _fixture.Build<ProcessQuery>()
                                .With(x => x.ProcessName, processResponse.ProcessName)
                                .With(x => x.Id, processResponse.Id)
                                .Create();
            var queryObject = _fixture.Create<UpdateProcessByIdRequestObject>();

            return (currentProcessState, query, queryObject);
        }

        #region Get By ID

        [Fact]
        public async Task GetProcessWithValidIDReturnsOKResponse()
        {
            //Arrange
            var stubHttpContext = new DefaultHttpContext();
            var controllerContext = new ControllerContext(new ActionContext(stubHttpContext, new RouteData(), new ControllerActionDescriptor()));
            _classUnderTest.ControllerContext = controllerContext;

            var process = _fixture.Create<Process>();
            var query = ConstructQuery(process.Id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync(process);

            //Act
            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as OkObjectResult;

            //Assert
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
            //Arrange
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync((Process) null);

            //Act
            var response = await _classUnderTest.GetProcessById(query).ConfigureAwait(false) as NotFoundObjectResult;

            //Assert
            response.StatusCode.Should().Be(404);
            response.Value.Should().Be(query.Id);
        }

        [Fact]
        public void GetProcessByIdExceptionIsThrown()
        {
            //Arrange
            var id = Guid.NewGuid();
            var query = ConstructQuery(id);
            var exception = new ApplicationException("Test exception");
            _mockGetByIdUseCase.Setup(x => x.Execute(query)).ThrowsAsync(exception);

            //Act
            Func<Task<IActionResult>> func = async () => await _classUnderTest.GetProcessById(query).ConfigureAwait(false);

            //Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        #endregion

        #region Create New Process

        [Fact]
        public async void CreateNewProcessReturnsCreatedResponse()
        {
            // Arrange
            var request = _fixture.Create<CreateProcess>();
            var processName = ProcessName.soletojoint;
            var processResponse = _fixture.Create<Process>();

            _mockCreateProcessUseCase.Setup(x => x.Execute(request, processName, It.IsAny<Token>())).ReturnsAsync(processResponse);

            // Act
            var response = await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);

            // Assert
            response.Should().BeOfType(typeof(CreatedResult));
            (response as CreatedResult).Value.Should().Be(processResponse);
        }

        [Theory]
        [InlineData(typeof(FormDataNotFoundException))]
        [InlineData(typeof(FormDataFormatException))]
        [InlineData(typeof(InvalidTriggerException))]
        public async Task CreateNewProcessReturnsBadRequest(Type exceptionType)
        {
            var request = _fixture.Create<CreateProcess>();
            var processName = ProcessName.soletojoint;
            var exception = Activator.CreateInstance(exceptionType) as Exception;

            _mockCreateProcessUseCase.Setup(x => x.Execute(request, processName, It.IsAny<Token>())).ThrowsAsync(exception);

            var response = await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);

            response.Should().BeOfType(typeof(BadRequestObjectResult));
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var request = _fixture.Create<CreateProcess>();
            var processName = ProcessName.soletojoint;
            var exception = new ApplicationException("Test exception");
            _mockCreateProcessUseCase.Setup(x => x.Execute(request, processName, It.IsAny<Token>())).ThrowsAsync(exception);

            Func<Task<IActionResult>> func = async () => await _classUnderTest.CreateNewProcess(request, processName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        #endregion

        #region Update Process

        [Fact]
        public async void UpdateProcessStateSuccessfullyReturnsUpdatedStatus()
        {
            // Arrange
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            _mockUpdateProcessUseCase.Setup(x => x.Execute(request, requestObject, It.IsAny<int?>(), It.IsAny<Token>()))
                                     .ReturnsAsync(processResponse);
            // Act
            var response = await _classUnderTest.UpdateProcessState(requestObject, request).ConfigureAwait(false);
            // Assert
            response.Should().BeOfType(typeof(NoContentResult));
        }

        [Fact]
        public async void UpdateProcessStateReturnsNotFoundWhenProcessDoesNotExist()
        {
            // Arrange
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            _mockUpdateProcessUseCase.Setup(x => x.Execute(request, requestObject, It.IsAny<int?>(), It.IsAny<Token>()))
                                     .ReturnsAsync((Process) null);
            // Act
            var response = await _classUnderTest.UpdateProcessState(requestObject, request).ConfigureAwait(false);
            // Assert
            response.Should().BeOfType(typeof(NotFoundObjectResult));
            (response as NotFoundObjectResult).Value.Should().Be(request.Id);
        }

        [Theory]
        [InlineData(typeof(FormDataInvalidException))]
        [InlineData(typeof(InvalidTriggerException))]
        public async Task UpdateProcessStateReturnsBadRequest(Type exceptionType)
        {
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            var exception = Activator.CreateInstance(exceptionType) as Exception;

            _mockUpdateProcessUseCase.Setup(x => x.Execute(request, requestObject, It.IsAny<int?>(), It.IsAny<Token>()))
                                     .ThrowsAsync(exception);

            var response = await _classUnderTest.UpdateProcessState(requestObject, request).ConfigureAwait(false);

            response.Should().BeOfType(typeof(BadRequestObjectResult));
        }

        [Fact]
        public async Task UpdateProcessStateReturnsConflict()
        {
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();
            var exception = new VersionNumberConflictException(1, 2);

            _mockUpdateProcessUseCase.Setup(x => x.Execute(request, requestObject, It.IsAny<int?>(), It.IsAny<Token>()))
                                     .ThrowsAsync(exception);

            var response = await _classUnderTest.UpdateProcessState(requestObject, request).ConfigureAwait(false);

            response.Should().BeOfType(typeof(ConflictObjectResult));
        }

        [Fact]
        public void UpdateProcessStateExceptionIsThrown()
        {
            // Arrange
            var exception = new ApplicationException("Test exception");
            (var processResponse, var request, var requestObject) = ConstructPatchRequest();

            _mockUpdateProcessUseCase.Setup(x => x.Execute(request, requestObject, It.IsAny<int?>(), It.IsAny<Token>()))
                                     .ThrowsAsync(exception);
            // Act
            Func<Task<IActionResult>> func = async () => await _classUnderTest.UpdateProcessState(requestObject, request).ConfigureAwait(false);
            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        #endregion

        #region Update Process by ID

        [Fact]
        public async void UpdateProcessbyIdSuccessfullyReturnsUpdatedStatus()
        {
            // Arrange
            (var processResponse, var request, var requestObject) = ConstructPatchByIdRequest();
            _mockUpdateProcessByIdUseCase.Setup(x => x.Execute(request, requestObject, RequestBodyText, It.IsAny<int?>(), It.IsAny<Token>()))
                                         .ReturnsAsync(processResponse);
            // Act
            var response = await _classUnderTest.UpdateProcessById(requestObject, request).ConfigureAwait(false);
            // Assert
            response.Should().BeOfType(typeof(NoContentResult));
        }

        [Fact]
        public async void UpdateProcessByIdReturnsNotFoundWhenProcessDoesNotExist()
        {
            // Arrange
            (var processResponse, var request, var requestObject) = ConstructPatchByIdRequest();
            _mockUpdateProcessByIdUseCase.Setup(x => x.Execute(request, requestObject, RequestBodyText, It.IsAny<int?>(), It.IsAny<Token>()))
                                         .ReturnsAsync((ProcessState) null);
            // Act
            var response = await _classUnderTest.UpdateProcessById(requestObject, request).ConfigureAwait(false);
            // Assert
            response.Should().BeOfType(typeof(NotFoundObjectResult));
            (response as NotFoundObjectResult).Value.Should().Be(request.Id);
        }

        [Fact]
        public async Task UpdateProcessByIdReturnsConflict()
        {
            (var processResponse, var request, var requestObject) = ConstructPatchByIdRequest();
            var exception = new VersionNumberConflictException(1, 2);
            _mockUpdateProcessByIdUseCase.Setup(x => x.Execute(request, requestObject, RequestBodyText, It.IsAny<int?>(), It.IsAny<Token>()))
                                         .ThrowsAsync(exception);

            var response = await _classUnderTest.UpdateProcessById(requestObject, request).ConfigureAwait(false);

            response.Should().BeOfType(typeof(ConflictObjectResult));
        }

        [Fact]
        public void UpdateProcessByIdExceptionIsThrown()
        {
            // Arrange
            var exception = new ApplicationException("Test exception");
            (var processResponse, var request, var requestObject) = ConstructPatchByIdRequest();
            _mockUpdateProcessByIdUseCase.Setup(x => x.Execute(request, requestObject, RequestBodyText, It.IsAny<int?>(), It.IsAny<Token>()))
                                         .ThrowsAsync(exception);
            // Act
            Func<Task<IActionResult>> func = async () => await _classUnderTest.UpdateProcessById(requestObject, request).ConfigureAwait(false);
            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        #endregion

        #region Get by Target Id

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("some-value")]
        public async Task GetProcessesByTargetIdReturnsOkWithNoResults(string paginationToken)
        {
            // Arrange
            var id = Guid.NewGuid();
            var query = new GetProcessesByTargetIdRequest { TargetId = id, PaginationToken = paginationToken };
            
            var result = _fixture.Build<PagedResult<ProcessResponse>>()
                                 .With(x => x.Results, new List<ProcessResponse>())
                                 .Create();
            _mockGetProcessByTargetIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync((PagedResult<ProcessResponse>) result);

            // Act
            var response = await _classUnderTest.GetByTargetId(query).ConfigureAwait(false);

            // Assert
            response.Should().BeOfType(typeof(OkObjectResult));
            (response as OkObjectResult).Value.Should().BeEquivalentTo(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("some-value")]
        public async Task GetProcessesByTargetIdReturnsProcesses(string paginationToken)
        {
            // Arrange
            var id = Guid.NewGuid();
            var query = new GetProcessesByTargetIdRequest { TargetId = id, PaginationToken = paginationToken };

            var processes = _fixture.CreateMany<ProcessResponse>(5).ToList();
            var pagedResult = new PagedResult<ProcessResponse>(processes, new PaginationDetails(paginationToken));
            _mockGetProcessByTargetIdUseCase.Setup(x => x.Execute(query)).ReturnsAsync(pagedResult);

            // Act
            var response = await _classUnderTest.GetByTargetId(query).ConfigureAwait(false);

            // Assert
            response.Should().BeOfType(typeof(OkObjectResult));
            (response as OkObjectResult).Value.Should().BeEquivalentTo(pagedResult);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("some-value")]
        public void GetProcessesByTargetIdExceptionIsThrown(string paginationToken)
        {
            // Arrange
            var id = Guid.NewGuid();
            var query = new GetProcessesByTargetIdRequest { TargetId = id, PaginationToken = paginationToken };
            var exception = new ApplicationException("Test exception");
            _mockGetProcessByTargetIdUseCase.Setup(x => x.Execute(query)).ThrowsAsync(exception);

            // Act
            Func<Task<IActionResult>> func = async () => await _classUnderTest.GetByTargetId(query).ConfigureAwait(false);

            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        #endregion
    }
}
