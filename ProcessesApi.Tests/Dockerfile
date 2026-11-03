FROM mcr.microsoft.com/dotnet/sdk:10.0


# disable microsoft telematry
ENV DOTNET_CLI_TELEMETRY_OPTOUT='true'

ENV DynamoDb_LocalMode='true'
ENV Sns_LocalMode='true'


ARG LBHPACKAGESTOKEN
ENV LBHPACKAGESTOKEN=$LBHPACKAGESTOKEN

WORKDIR /app

# Copy csproj and nuget config and restore as distinct layers
COPY ./ProcessesApi.sln ./
COPY ./ProcessesApi/ProcessesApi.csproj ./ProcessesApi/
COPY ./ProcessesApi.Tests/ProcessesApi.Tests.csproj ./ProcessesApi.Tests/
COPY /NuGet.Config /root/.nuget/NuGet/NuGet.Config

RUN dotnet restore ./ProcessesApi/ProcessesApi.csproj
RUN dotnet restore ./ProcessesApi.Tests/ProcessesApi.Tests.csproj

# Copy everything else and build
COPY . .

RUN dotnet build -c Release -o out ProcessesApi/ProcessesApi.csproj
RUN dotnet build -c debug -o out ProcessesApi.Tests/ProcessesApi.Tests.csproj

CMD dotnet test
