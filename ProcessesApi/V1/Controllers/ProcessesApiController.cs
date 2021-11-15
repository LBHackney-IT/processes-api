using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.UseCase.Interfaces;
using ProcessesApi.V1.Factories;
using Hackney.Core.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace ProcessesApi.V1.Controllers
{
    [ApiController]
    [Route("api/v1/process")]
    [Produces("application/json")]
    [ApiVersion("1.0")]
    public class ProcessesApiController : BaseController
    {
        private readonly IGetByIdUseCase _getByIdUseCase;
        public ProcessesApiController(IGetByIdUseCase getByIdUseCase)
        {
            _getByIdUseCase = getByIdUseCase;
        }

        /// <summary>
        /// Retrieve all details about a particular process
        /// </summary>
        /// <response code="200">Successfully retrieved details for a particular process</response>
        /// <response code="404">No process found for the specified ID</response> 
        /// <response code="500">Something went wrong</response>
        [ProducesResponseType(typeof(ProcessesResponse), StatusCodes.Status200OK)]
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
            
            HttpContext.Response.Headers.Add(HeaderConstants.ETag, EntityTagHeaderValue.Parse($"\"{eTag}\"").Tag);
            
            return Ok(process.ToResponse());
        }
    }
}
