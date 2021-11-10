using AutoFixture;
using ProcessesApi.V1.Controllers;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.UseCase.Interfaces;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.Tests.V1.Controllers
{
    [Collection("LogCall collection")]
    public class ProcessesApiControllerTests
    {
        private ProcessesApiController _classUnderTest;
        private Mock<IGetByIdUseCase> _mockGetByIdUseCase;

        public ProcessesApiControllerTests()
        {
            _mockGetByIdUseCase = new Mock<IGetByIdUseCase>();
            _classUnderTest = new ProcessesApiController(_mockGetByIdUseCase.Object);
        }


        //Add Tests Here
    }
}
