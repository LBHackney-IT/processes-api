using Hackney.Core.DynamoDb;
using Hackney.Core.Http;
using Hackney.Core.JWT;
using Hackney.Core.Logging;
using Hackney.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Boundary.Response;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Factories;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using HeaderConstants = Hackney.Shared.Processes.Boundary.Constants.HeaderConstants;
using Hackney.Shared.Processes.Boundary.Request.V1;

namespace ProcessesApi.V1.Controllers
{
    [ApiController]
    [Route("api/v1/process")]
    [Produces("application/json")]
    [ApiVersion("1.0")]
    public class ProcessesApiController : BaseController
    {
        private readonly IGetByIdUseCase _getByIdUseCase;
        private readonly ICreateProcessUseCase _createProcessUseCase;
        private readonly IUpdateProcessUseCase _updateProcessUseCase;
        private readonly IUpdateProcessByIdUseCase _updateProcessByIdUsecase;
        private readonly IGetProcessesByTargetIdUseCase _getProcessesByTargetIdUseCase;
        private readonly IHttpContextWrapper _contextWrapper;
        private readonly ITokenFactory _tokenFactory;


        public ProcessesApiController(IGetByIdUseCase getByIdUseCase,
                                      ICreateProcessUseCase createProcessUseCase,
                                      IUpdateProcessUseCase updateProcessUseCase,
                                      IUpdateProcessByIdUseCase updateProcessByIdUsecase,
                                      IGetProcessesByTargetIdUseCase getProcessesByTargetIdUseCase,
                                      IHttpContextWrapper contextWrapper,
                                      ITokenFactory tokenFactory)

        {
            _getByIdUseCase = getByIdUseCase;
            _createProcessUseCase = createProcessUseCase;
            _updateProcessUseCase = updateProcessUseCase;
            _updateProcessByIdUsecase = updateProcessByIdUsecase;
            _getProcessesByTargetIdUseCase = getProcessesByTargetIdUseCase;
            _contextWrapper = contextWrapper;
            _tokenFactory = tokenFactory;
        }

        /// <summary>
        /// Retrieve all processes with a specific target ID
        /// </summary>
        /// <response code="200">Sucessfully found processes requested </response>
        /// <response code="500">Internal Server Error</response>
        [ProducesResponseType(typeof(PagedResult<ProcessResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet]
        [LogCall(LogLevel.Information)]
        public async Task<IActionResult> GetByTargetId([FromQuery] GetProcessesByTargetIdRequest request)
        {
            var result = await _getProcessesByTargetIdUseCase.Execute(request).ConfigureAwait(false);
            return Ok(result);
        }

        /// <summary>
        /// Retrieve all details about a particular process
        /// </summary>
        /// <response code="200">Successfully retrieved details for a particular process</response>
        /// <response code="404">No process found for the specified ID</response> 
        /// <response code="500">Internal Server Error</response>
        [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet]
        [LogCall(LogLevel.Information)]
        [Route("{processName}/{id}")]
        public async Task<IActionResult> GetProcessById([FromRoute] ProcessQuery query)
        {
            var process = await _getByIdUseCase.Execute(query).ConfigureAwait(false);
            if (process == null) return NotFound(query.Id);

            var eTag = string.Empty;
            if (process.VersionNumber.HasValue)
                eTag = process.VersionNumber.ToString();

            HttpContext.Response.Headers.Add(HeaderConstants.ETag, EntityTagHeaderValue.Parse($"\"{eTag}\"").Tag);

            return Ok(process.ToResponse());
        }

        /// <summary>
        /// Create a new process
        /// </summary>
        /// <response code="201">Process has been created successfully</response>
        /// <response code="400">Bad Request</response> 
        /// <response code="500">Internal Server Error</response>
        [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost]
        [LogCall(LogLevel.Information)]
        [Route("{processName}")]
        public async Task<IActionResult> CreateNewProcess([FromBody] CreateProcess request, [FromRoute] ProcessName processName)
        {
            var token = _tokenFactory.Create(_contextWrapper.GetContextRequestHeaders(HttpContext));
            try
            {
                var result = await _createProcessUseCase.Execute(request, processName, token).ConfigureAwait(false);
                return Created(new Uri($"api/v1/processes/{processName}/{result.Id}", UriKind.Relative), result);
            }
            catch (Exception ex) when (ex is FormDataInvalidException
                                      || ex is InvalidTriggerException)
            {
                return BadRequest(ex);
            }
        }

        /// <summary>
        /// Update a process's state
        /// </summary>
        /// <response code="204">Process has been updated successfully</response>
        /// <response code="400">Bad Request</response> 
        /// <response code="404">Not Found</response> 
        /// <response code="500">Internal Server Error</response>
        [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPatch]
        [LogCall(LogLevel.Information)]
        [Route("{processName}/{id}/{processTrigger}")]
        public async Task<IActionResult> UpdateProcessState([FromBody] UpdateProcessRequestObject requestObject, [FromRoute] UpdateProcessQuery query)
        {
            var token = _tokenFactory.Create(_contextWrapper.GetContextRequestHeaders(HttpContext));
            _contextWrapper.GetContextRequestHeaders(HttpContext);
            var ifMatch = GetIfMatchFromHeader();

            try
            {
                var result = await _updateProcessUseCase.Execute(query, requestObject, ifMatch, token);
                if (result == null) return NotFound(query.Id);
                return NoContent();
            }
            catch (VersionNumberConflictException vncErr)
            {
                return Conflict(vncErr.Message);
            }
            catch (Exception ex) when (ex is FormDataInvalidException
                                      || ex is InvalidTriggerException)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Update a process
        /// </summary>
        /// <response code="204">Process has been updated successfully</response>
        /// <response code="400">Bad Request</response> 
        /// <response code="404">Not Found</response> 
        /// <response code="500">Internal Server Error</response>
        [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPatch]
        [LogCall(LogLevel.Information)]
        [Route("{processName}/{id}")]
        public async Task<IActionResult> UpdateProcessById([FromBody] UpdateProcessByIdRequestObject requestObject, [FromRoute] ProcessQuery query)
        {
            // This is only possible because the EnableRequestBodyRewind middleware is specified in the application startup.
            var bodyText = await HttpContext.Request.GetRawBodyStringAsync().ConfigureAwait(false);
            var token = _tokenFactory.Create(_contextWrapper.GetContextRequestHeaders(HttpContext));
            var ifMatch = GetIfMatchFromHeader();
            try
            {
                // We use a request object AND the raw request body text because the incoming request will only contain the fields that changed
                // whereas the request object has all possible updateable fields defined.
                // The implementation will use the raw body text to identify which fields to update and the request object is specified here so that its
                // associated validation will be executed by the MVC pipeline before we even get to this point.
                var response = await _updateProcessByIdUsecase.Execute(query, requestObject, bodyText, ifMatch, token);
                if (response == null) return NotFound(query.Id);
                return NoContent();
            }
            catch (VersionNumberConflictException vncErr)
            {
                return Conflict(vncErr.Message);
            }
        }

        private int? GetIfMatchFromHeader()
        {
            var header = HttpContext.Request.Headers.GetHeaderValue(HeaderConstants.IfMatch);

            if (header == null)
                return null;

            _ = EntityTagHeaderValue.TryParse(header, out var entityTagHeaderValue);

            if (entityTagHeaderValue == null)
                return null;

            var version = entityTagHeaderValue.Tag.Replace("\"", string.Empty);

            if (int.TryParse(version, out var numericValue))
                return numericValue;

            return null;
        }
    }
}
