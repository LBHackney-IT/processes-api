using Hackney.Core.Http;
using Hackney.Core.Logging;
using Hackney.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Controllers
{
    [ApiController]
    [Route("api/v1/process")]
    [Produces("application/json")]
    [ApiVersion("1.0")]
    public class ProcessesApiController : BaseController
    {
        private readonly IGetByIdUseCase _getByIdUseCase;
        private readonly ISoleToJointUseCase _soleToJointUseCase;
        private readonly IHttpContextWrapper _contextWrapper;


        public ProcessesApiController(IGetByIdUseCase getByIdUseCase, ISoleToJointUseCase soleToJointUseCase, IHttpContextWrapper contextWrapper)
        {
            _getByIdUseCase = getByIdUseCase;
            _soleToJointUseCase = soleToJointUseCase;
            _contextWrapper = contextWrapper;
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
        [Route("{process-name}/{id}")]
        public async Task<IActionResult> GetProcessById([FromRoute] ProcessesQuery query)
        {
            var process = await _getByIdUseCase.Execute(query).ConfigureAwait(false);
            if (process == null) return NotFound(query.Id);

            var eTag = string.Empty;
            if (process.VersionNumber.HasValue)
                eTag = process.VersionNumber.ToString();

            HttpContext.Response.Headers.Add(Boundary.Constants.HeaderConstants.ETag, EntityTagHeaderValue.Parse($"\"{eTag}\"").Tag);

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
        public async Task<IActionResult> CreateNewProcess([FromBody] CreateProcess request, [FromRoute] string processName)
        {
            switch (processName)
            {
                case ProcessNamesConstants.SoleToJoint:
                    var soleToJointResult = await _soleToJointUseCase.Execute(
                                                                      Guid.NewGuid(),
                                                                      SoleToJointInternalTriggers.StartApplication,
                                                                      request.TargetId,
                                                                      request.RelatedEntities,
                                                                      request.FormData,
                                                                      request.Documents,
                                                                      processName,
                                                                      null)
                                                                     .ConfigureAwait(false);

                    return Created(new Uri($"api/v1/processes/{processName}/{soleToJointResult.Id}", UriKind.Relative), soleToJointResult);
                default:
                    var error = new ErrorResponse
                    {
                        ErrorCode = 1,
                        ProcessId = Guid.Empty,
                        ErrorMessage = "Process type does not exist",
                        ProcessName = processName
                    };
                    return new BadRequestObjectResult(error);
            }

        }

        /// <summary>
        /// Update a process's state
        /// </summary>
        /// <response code="204">Process has been updated successfully</response>
        /// <response code="400">Bad Request</response> 
        /// <response code="404">Not Found</response> 
        /// <response code="500">Internal Server Error</response>
        [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPatch]
        [LogCall(LogLevel.Information)]
        [Route("{processName}/{id}/{processTrigger}")]
        public async Task<IActionResult> UpdateProcess([FromBody] UpdateProcessQueryObject requestObject, [FromRoute] UpdateProcessQuery query)
        {
            _contextWrapper.GetContextRequestHeaders(HttpContext);
            //TO DO: Complete ifMatch stuff
            var ifMatch = GetIfMatchFromHeader();
            try
            {
                var soleToJointResult = await _soleToJointUseCase.Execute(query.Id,
                                                                          query.ProcessTrigger,
                                                                          null,
                                                                          null,
                                                                          requestObject.FormData,
                                                                          requestObject.Documents,
                                                                          query.ProcessName,
                                                                          ifMatch);
                if (soleToJointResult == null) return NotFound(query.Id);
                return NoContent();
            }
            catch (VersionNumberConflictException vncErr)
            {
                return Conflict(vncErr.Message);
            }
        }

        private int? GetIfMatchFromHeader()
        {
            var header = HttpContext.Request.Headers.GetHeaderValue(Boundary.Constants.HeaderConstants.IfMatch);

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
