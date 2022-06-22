FROM mcr.microsoft.com/dotnet/core/sdk:3.1


# disable microsoft telematry
ENV DOTNET_CLI_TELEMETRY_OPTOUT='true'

ENV DynamoDb_LocalMode='true'
ENV Sns_LocalMode='true'


ARG LBHPACKAGESTOKEN
ENV LBHPACKAGESTOKEN=$LBHPACKAGESTOKEN

ARG SONAR_TOKEN
ENV SONAR_TOKEN=$SONAR_TOKEN


WORKDIR /app

# Install and run sonar cloud scanner
RUN apt-get update && apt-get install -y openjdk-11-jdk
RUN dotnet tool install --global dotnet-sonarscanner --version 5.6.0
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet sonarscanner begin /k:"LBHackney-IT_processes-api" /o:"lbhackney-it" /d:sonar.host.url=https://sonarcloud.io /d:sonar.login="${SONAR_TOKEN}"

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
RUN dotnet sonarscanner end /d:sonar.login="${SONAR_TOKEN}"
