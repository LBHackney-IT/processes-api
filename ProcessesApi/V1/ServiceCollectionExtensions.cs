using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Services;
using ProcessesApi.V1.Services.Interfaces;

namespace ProcessesApi.V1
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigureProcessServices(this IServiceCollection services)
        {
            services.AddTransient<SoleToJointService>();
            // List Process Services here

            services.AddTransient<Func<ProcessName, IProcessService>>(serviceProvider => (processName) =>
            {
                switch (processName)
                {
                    case ProcessName.soletojoint:
                        return serviceProvider.GetRequiredService<SoleToJointService>();
                    default:
                        throw new InvalidEnumArgumentException(nameof(ProcessName), (int) processName, typeof(ProcessName));
                }
            });
        }
    }
}
