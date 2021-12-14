using Amazon.DynamoDBv2;
using Hackney.Core.DynamoDb;
using Hackney.Core.Testing.DynamoDb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ProcessesApi.Tests
{
    public class MockWebApplicationFactory<TStartup>
        : WebApplicationFactory<TStartup> where TStartup : class
    {
        private readonly List<TableDef> _tables = new List<TableDef>
            {
                new TableDef
                {
                    Name = "Processes",
                    KeyName = "id",
                    KeyType = ScalarAttributeType.S,
                },
                new TableDef
                {
                    Name = "TenureInformation",
                    KeyName = "id",
                    KeyType = ScalarAttributeType.S,
                }
            };

        public IDynamoDbFixture DynamoDbFixture { get; private set; }
        public HttpClient Client { get; private set; }

        public MockWebApplicationFactory()
        {
            EnsureEnvVarConfigured("DynamoDb_LocalMode", "true");
            EnsureEnvVarConfigured("DynamoDb_LocalServiceUrl", "http://localhost:8000");

            Client = CreateClient();
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposing && _disposed)
            {
                if (DynamoDbFixture != null)
                    DynamoDbFixture.Dispose();
                if (Client != null)
                    Client.Dispose();

                base.Dispose(disposing);

                _disposed = true;
            }
        }

        private static void EnsureEnvVarConfigured(string name, string defaultValue)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, defaultValue);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(b => b.AddEnvironmentVariables())
                .UseStartup<Startup>();
            builder.ConfigureServices(services =>
            {
                services.ConfigureDynamoDB();
                services.ConfigureDynamoDbFixture();

                var serviceProvider = services.BuildServiceProvider();
                DynamoDbFixture = serviceProvider.GetRequiredService<IDynamoDbFixture>();

                DynamoDbFixture.EnsureTablesExist(_tables);
            });
        }
    }
}
