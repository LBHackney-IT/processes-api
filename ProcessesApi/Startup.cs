using Amazon;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Hackney.Core.DynamoDb;
using Hackney.Core.DynamoDb.HealthCheck;
using Hackney.Core.HealthCheck;
using Hackney.Core.Http;
using Hackney.Core.JWT;
using Hackney.Core.Logging;
using Hackney.Core.Middleware.CorrelationId;
using Hackney.Core.Middleware.Exception;
using Hackney.Core.Middleware.Logging;
using Hackney.Core.Middleware;
using Hackney.Core.Sns;
using Hackney.Core.Validation.AspNet;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Hackney.Shared.Processes.Boundary.Request.Validation;
using ProcessesApi.V1.Gateways;
using Hackney.Shared.Processes.Infrastructure;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.UseCase.Interfaces;
using ProcessesApi.Versioning;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Factories;

namespace ProcessesApi
{
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            AWSSDKHandler.RegisterXRayForAllServices();
        }

        public IConfiguration Configuration { get; }
        private static List<ApiVersionDescription> _apiVersions { get; set; }
        private const string ApiName = "processes";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddHttpClient();
            services.AddApiGateway();

            services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            services.AddFluentValidation(Assembly.GetAssembly(typeof(ProcessDataValidator)));

            services.AddApiVersioning(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.AssumeDefaultVersionWhenUnspecified = true; // assume that the caller wants the default version if they don't specify
                o.ApiVersionReader = new UrlSegmentApiVersionReader(); // read the version number from the url segment header)
            });

            services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();

            services.AddDynamoDbHealthCheck<ProcessesDb>();

            services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Token",
                    new OpenApiSecurityScheme
                    {
                        In = ParameterLocation.Header,
                        Description = "Your Hackney API Key",
                        Name = "X-Api-Key",
                        Type = SecuritySchemeType.ApiKey
                    });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Token" }
                        },
                        new List<string>()
                    }
                });

                //Looks at the APIVersionAttribute [ApiVersion("x")] on controllers and decides whether or not
                //to include it in that version of the swagger document
                //Controllers must have this [ApiVersion("x")] to be included in swagger documentation!!
                c.DocInclusionPredicate((docName, apiDesc) =>
                {
                    apiDesc.TryGetMethodInfo(out var methodInfo);

                    var versions = methodInfo?
                        .DeclaringType?.GetCustomAttributes()
                        .OfType<ApiVersionAttribute>()
                        .SelectMany(attr => attr.Versions).ToList();

                    return versions?.Any(v => $"{v.GetFormattedApiVersion()}" == docName) ?? false;
                });

                //Get every ApiVersion attribute specified and create swagger docs for them
                foreach (var apiVersion in _apiVersions)
                {
                    var version = $"v{apiVersion.ApiVersion.ToString()}";
                    c.SwaggerDoc(version, new OpenApiInfo
                    {
                        Title = $"{ApiName}-api {version}",
                        Version = version,
                        Description = $"{ApiName} version {version}. Please check older versions for depreciated endpoints."
                    });
                }

                c.CustomSchemaIds(x => x.FullName);
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                    c.IncludeXmlComments(xmlPath);
            });

            services.ConfigureLambdaLogging(Configuration);

            AWSXRayRecorder.InitializeInstance(Configuration);
            AWSXRayRecorder.RegisterLogger(LoggingOptions.SystemDiagnostics);

            services.ConfigureDynamoDB();
            services.ConfigureSns();
            services.AddLogCallAspect();

            services.ConfigureProcessServices();

            RegisterGateways(services);
            RegisterUseCases(services);
            RegisterHelpers(services);

            services.AddSingleton<IConfiguration>(Configuration);

            services.AddScoped<IPersonSnsFactory, PersonSnsFactory>();
            services.AddScoped<ITenureSnsFactory, TenureSnsFactory>();

            ConfigureHackneyCoreDI(services);

        }

        private static void ConfigureHackneyCoreDI(IServiceCollection services)
        {
            services.AddSnsGateway()
                .AddTokenFactory()
                .AddHttpContextWrapper();
        }



        private static void RegisterGateways(IServiceCollection services)
        {
            services.AddScoped<IProcessesGateway, ProcessesGateway>();
            services.AddScoped<ITenureDbGateway, TenureDbGateway>();
            services.AddScoped<IPersonDbGateway, PersonDbGateway>();
            services.AddScoped<IIncomeApiGateway, IncomeApiGateway>();
            services.AddScoped<IEntityUpdater, EntityUpdater>();
        }

        private static void RegisterUseCases(IServiceCollection services)
        {
            services.AddScoped<IGetByIdUseCase, GetProcessByIdUseCase>();
            services.AddScoped<V1.UseCase.Interfaces.ICreateProcessUseCase, V1.UseCase.CreateProcessUseCase>();
            services.AddScoped<IUpdateProcessUseCase, UpdateProcessUseCase>();
            services.AddScoped<IUpdateProcessByIdUseCase, UpdateProcessByIdUseCase>();
            services.AddScoped<IGetProcessesByTargetIdUseCase, GetProcessesByTargetIdUseCase>();

            services.AddScoped<V2.UseCase.Interfaces.ICreateProcessUseCase, V2.UseCase.CreateProcessUseCase>();
        }

        private static void RegisterHelpers(IServiceCollection services)
        {
            services.AddScoped<IDbOperationsHelper, DbOperationsHelper>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseCors(builder => builder
                  .AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("ETag", "If-Match", "x-correlation-id"));


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseCorrelationId();
            app.UseLoggingScope();
            app.UseCustomExceptionHandler(logger);
            app.UseXRay("processes-api");
            app.EnableRequestBodyRewind();

            //Get All ApiVersions,
            var api = app.ApplicationServices.GetService<IApiVersionDescriptionProvider>();
            _apiVersions = api.ApiVersionDescriptions.ToList();

            //Swagger ui to view the swagger.json file
            app.UseSwaggerUI(c =>
            {
                foreach (var apiVersionDescription in _apiVersions)
                {
                    //Create a swagger endpoint for each swagger version
                    c.SwaggerEndpoint($"{apiVersionDescription.GetFormattedApiVersion()}/swagger.json",
                        $"{ApiName}-api {apiVersionDescription.GetFormattedApiVersion()}");
                }
            });
            app.UseSwagger();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                // SwaggerGen won't find controllers that are routed via this technique.
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapHealthChecks("/api/v1/healthcheck/ping", new HealthCheckOptions()
                {
                    ResponseWriter = HealthCheckResponseWriter.WriteResponse
                });
            });
            app.UseLogCall();
        }
    }
}
