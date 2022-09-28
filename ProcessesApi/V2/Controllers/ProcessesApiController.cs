using Hackney.Core.Http;
using Hackney.Core.JWT;
using Hackney.Core.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Processes.Boundary.Response;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V2.UseCase.Interfaces;
using System;
using System.Threading.Tasks;
using Hackney.Shared.Processes.Boundary.Request.V2;

namespace ProcessesApi.V2.Controllers
{
    [ApiController]
    [Route("api/v2/process")]
    [Produces("application/json")]
    [ApiVersion("1.0")]
    public class ProcessesApiController : BaseController
    {
        private readonly ICreateProcessUseCase _createProcessUseCase;
        private readonly IHttpContextWrapper _contextWrapper;
        private readonly ITokenFactory _tokenFactory;


        public ProcessesApiController(ICreateProcessUseCase createProcessUseCase,
                                      IHttpContextWrapper contextWrapper,
                                      ITokenFactory tokenFactory)

        {
            _createProcessUseCase = createProcessUseCase;
            _contextWrapper = contextWrapper;
            _tokenFactory = tokenFactory;
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
                return Created(new Uri($"api/v1/process/{processName}/{result.Id}", UriKind.Relative), result);
            }
            catch (Exception ex) when (ex is FormDataInvalidException
                                      || ex is InvalidTriggerException)
            {
                return BadRequest(ex);
            }
        }
    }
}
