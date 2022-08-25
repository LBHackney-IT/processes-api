using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Services;
using ProcessesApi.V2.Services.Interfaces;

namespace ProcessesApi.V2
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
