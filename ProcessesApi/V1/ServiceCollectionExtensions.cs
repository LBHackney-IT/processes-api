using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Services;
using ProcessesApi.V1.Services.Interfaces;

namespace ProcessesApi.V1
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigureProcessServices(this IServiceCollection services)
        {
            services.AddTransient<SoleToJointService>();
            services.AddTransient<ChangeOfNameService>();
            // List Process Services here

            services.AddTransient<Func<ProcessName, IProcessService>>(serviceProvider => (processName) =>
            {
                switch (processName)
                {
                    case ProcessName.soletojoint:
                        return serviceProvider.GetRequiredService<SoleToJointService>();
                    case ProcessName.changeofname:
                        return serviceProvider.GetRequiredService<ChangeOfNameService>();
                    default:
                        throw new InvalidEnumArgumentException(nameof(ProcessName), (int) processName, typeof(ProcessName));
                }
            });
        }
    }
}
