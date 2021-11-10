using AutoFixture;
using ProcessesApi.V1.Controllers;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.UseCase.Interfaces;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.Tests.V1.Controllers
{
    [TestFixture]
    public class ProcessesApiControllerTests : LogCallAspectFixture
    {
        private ProcessesApiController _classUnderTest;
        private Mock<IGetByIdUseCase> _mockGetByIdUseCase;

        [SetUp]
        public void SetUp()
        {
            _mockGetByIdUseCase = new Mock<IGetByIdUseCase>();
            _classUnderTest = new ProcessesApiController(_mockGetByIdUseCase.Object);
        }


        //Add Tests Here
    }
}
