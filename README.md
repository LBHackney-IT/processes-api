# Processes API

Processes API manages housing workflows, including process creation, retrieval, updates, state transitions, and related events. It is an ASP.NET Core API targeting .NET 10 and runs as an AWS Lambda service or in Docker.

## Technology and data

-   .NET 10 / ASP.NET Core
-   DynamoDB for process, person, and tenure records
-   Amazon SNS for process, person, and tenure events
-   Income API for income-related checks
-   Stateless for workflow state machines
-   xUnit for automated tests

The API reads and writes process data through DynamoDB. It also integrates with the Income API and publishes SNS events. The Person and Tenure gateways use DynamoDB-backed records. In local Docker development, LocalStack provides SNS and DynamoDB Local provides a local DynamoDB endpoint.

## Requirements

-   .NET 10 SDK for local builds
-   Docker with Docker Compose v2 for container-based development and tests
-   Access to the Hackney GitHub NuGet feed

Set `LBHPACKAGESTOKEN` to a GitHub package token with permission to read the required packages. Docker builds pass this value as a build argument. Local `dotnet restore` also needs this environment variable because `NuGet.Config` uses it for the private package source.

## Build and run

Restore and build the solution:

```sh
export LBHPACKAGESTOKEN=<token>
dotnet restore ProcessesApi.sln
dotnet build ProcessesApi.sln
```

Run the API with Docker Compose:

```sh
export LBHPACKAGESTOKEN=<token>
make build
make serve
```

The API listens on port 3000. The Docker Compose application configuration uses DynamoDB Local and LocalStack for local AWS-compatible services. The Income and Tenure API URLs and tokens are configured through application settings; use suitable local or development values when running outside the supplied Compose setup.

## API

The API uses JSON request and response bodies. Process routes require the relevant API authorization context in deployed environments. The deployment configures an API Gateway request authorizer; local test configuration supplies AWS-compatible local services.

| Method  | Path                                                  | Purpose                                                                                                                                  |
| ------- | ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`   | `/api/v1/process?targetId={targetId}`                 | List processes for a target. Optional `pageSize` and `paginationToken` query parameters control pagination; the default page size is 10. |
| `GET`   | `/api/v1/process/{processName}/{id}`                  | Retrieve a process and its current state. The response includes an `ETag` version.                                                       |
| `POST`  | `/api/v1/process/{processName}`                       | Create a process from the supplied process data.                                                                                         |
| `PATCH` | `/api/v1/process/{processName}/{id}/{processTrigger}` | Apply a workflow trigger and update the process state. Supports the `If-Match` version header.                                           |
| `PATCH` | `/api/v1/process/{processName}/{id}`                  | Update process fields by ID. Supports the `If-Match` version header.                                                                     |
| `POST`  | `/api/v2/process/{processName}`                       | Create a process using the v2 request contract.                                                                                          |
| `GET`   | `/api/v1/healthcheck/ping`                            | Health check.                                                                                                                            |

Swagger UI is served at `/swagger`. Request and response schemas are available in the generated Swagger document. Process names, trigger values, and body schemas depend on the selected workflow and are validated by the API.

## Tests

The `make test` target builds and runs the test container and starts its local DynamoDB and LocalStack dependencies. Supply the package-feed token; local tests do not require SonarCloud credentials:

```sh
export LBHPACKAGESTOKEN=<token>
make test
```

The test suite includes unit, gateway, and end-to-end tests. Tests use DynamoDB Local and LocalStack-backed SNS fixtures.

CircleCI runs SonarCloud analysis separately in the `sonar-analysis` job using SonarScanner for .NET 11.3.0 with automatic Java provisioning. The job uses the existing `SONAR_TOKEN` from the SonarCloud context. Both tests and analysis must succeed before deployment.

## Deployment

The service is packaged for AWS Lambda using the .NET 10 runtime. `ProcessesApi/build.sh` and `ProcessesApi/build.cmd` create the deployment archive referenced by `ProcessesApi/serverless.yml`. CI builds with the .NET 10 SDK.
