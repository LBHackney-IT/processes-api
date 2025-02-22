using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackney.Core.DynamoDb;
using Hackney.Core.Sns;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ProcessesApi.Tests
{
    public class AwsMockWebApplicationFactory<TStartup>
        : WebApplicationFactory<TStartup> where TStartup : class
    {
        private readonly List<TableDef> _tables = new List<TableDef>
            {
                new TableDef
                {
                    Name = "Processes",
                    KeyName = "id",
                    KeyType = ScalarAttributeType.S,
                    GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                    {
                        new GlobalSecondaryIndex
                        {
                            IndexName = "ProcessesByTargetId",
                            KeySchema = new List<KeySchemaElement>
                            {
                                new KeySchemaElement("targetId", KeyType.HASH),
                                new KeySchemaElement("id", KeyType.RANGE)
                            },
                            Projection = new Projection { ProjectionType = ProjectionType.ALL },
                            ProvisionedThroughput = new ProvisionedThroughput(3, 3)
                        }
                    }
                },
                new TableDef
                {
                    Name = "TenureInformation",
                    KeyName = "id",
                    KeyType = ScalarAttributeType.S,
                },
                new TableDef
                {
                    Name = "Persons",
                    KeyName = "id",
                    KeyType = ScalarAttributeType.S,
                }
            };

        public IDynamoDbFixture DynamoDbFixture { get; private set; }
        public HttpClient Client { get; private set; }
        public ISnsFixture SnsFixture { get; private set; }


        public AwsMockWebApplicationFactory()
        {
            EnsureEnvVarConfigured("DynamoDb_LocalMode", "true");
            EnsureEnvVarConfigured("DynamoDb_LocalServiceUrl", "http://localhost:8000");

            EnsureEnvVarConfigured("Sns_LocalMode", "true");
            EnsureEnvVarConfigured("Localstack_SnsServiceUrl", "http://localhost:4566");

            EnsureEnvVarConfigured("AWS_REGION", "eu-west-2");
            EnsureEnvVarConfigured("AWS_ACCESS_KEY_ID", "local");
            EnsureEnvVarConfigured("AWS_SECRET_ACCESS_KEY", "local");

            Client = CreateClient();
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                DynamoDbFixture?.Dispose();
                SnsFixture?.Dispose();
                Client?.Dispose();
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
                services.ConfigureSns();
                services.ConfigureSnsFixture();


                var serviceProvider = services.BuildServiceProvider();

                DynamoDbFixture = serviceProvider.GetRequiredService<IDynamoDbFixture>();
                DynamoDbFixture.EnsureTablesExist(_tables);

                SnsFixture = serviceProvider.GetRequiredService<ISnsFixture>();
                SnsFixture.CreateSnsTopic<EntityEventSns>("processes.fifo", "PROCESS_SNS_ARN");
                SnsFixture.CreateSnsTopic<EntityEventSns>("tenure.fifo", "TENURE_SNS_ARN");
                SnsFixture.CreateSnsTopic<EntityEventSns>("person.fifo", "PERSON_SNS_ARN");
            });
        }
    }
}
