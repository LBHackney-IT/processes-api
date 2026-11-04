FROM mcr.microsoft.com/dotnet/sdk:10.0


# disable microsoft telematry
ENV DOTNET_CLI_TELEMETRY_OPTOUT='true'

ENV DynamoDb_LocalMode='true'
ENV Sns_LocalMode='true'


ARG LBHPACKAGESTOKEN
ENV LBHPACKAGESTOKEN=$LBHPACKAGESTOKEN

ARG SONAR_TOKEN

WORKDIR /app

# Install and run the .NET scanner, which provisions its own Java runtime.
RUN dotnet tool install --global dotnet-sonarscanner --version 11.3.0 --source https://api.nuget.org/v3/index.json
ENV PATH="$PATH:/root/.dotnet/tools"

# CI supplies SONAR_TOKEN; local builds can run without uploading an analysis.
RUN if [ -n "$SONAR_TOKEN" ]; then \
      dotnet sonarscanner begin /k:"LBHackney-IT_processes-api" /o:"lbhackney-it" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.token="$SONAR_TOKEN"; \
    fi

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

RUN if [ -n "$SONAR_TOKEN" ]; then \
      dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"; \
    fi
