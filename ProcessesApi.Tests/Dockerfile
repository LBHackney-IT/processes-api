FROM mcr.microsoft.com/dotnet/sdk:10.0

ENV DOTNET_CLI_TELEMETRY_OPTOUT=true \
    DynamoDb_LocalMode=true \
    Sns_LocalMode=true

RUN install -d -o app -g app /app /home/app/.nuget /home/app/.nuget/NuGet /home/app/.nuget/packages
USER app
WORKDIR /app

# Install the scanner as the same non-root user that builds and runs tests.
ENV PATH="$PATH:/home/app/.dotnet/tools"
RUN dotnet tool install --global dotnet-sonarscanner --version 11.3.0 --source https://api.nuget.org/v3/index.json

ARG LBHPACKAGESTOKEN
ARG SONAR_TOKEN

# CI supplies SONAR_TOKEN; the scanner provisions its own Java runtime.
RUN if [ -n "$SONAR_TOKEN" ]; then \
      dotnet sonarscanner begin /k:"LBHackney-IT_processes-api" /o:"lbhackney-it" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.token="$SONAR_TOKEN"; \
    fi

# Restore project dependencies before copying source code.
COPY --chown=app:app ./ProcessesApi.sln ./
COPY --chown=app:app ./ProcessesApi/ProcessesApi.csproj ./ProcessesApi/
COPY --chown=app:app ./ProcessesApi.Tests/ProcessesApi.Tests.csproj ./ProcessesApi.Tests/
COPY --chown=app:app ./NuGet.Config /home/app/.nuget/NuGet/NuGet.Config
RUN dotnet restore ./ProcessesApi/ProcessesApi.csproj \
    && dotnet restore ./ProcessesApi.Tests/ProcessesApi.Tests.csproj

# Copy only the projects needed for testing, not the repository or local credentials.
COPY --chown=app:app ./.editorconfig ./
COPY --chown=app:app ./ProcessesApi/ ./ProcessesApi/
COPY --chown=app:app ./ProcessesApi.Tests/ ./ProcessesApi.Tests/
RUN dotnet build -c Release -o out ProcessesApi/ProcessesApi.csproj \
    && dotnet build -c Debug -o out ProcessesApi.Tests/ProcessesApi.Tests.csproj \
    && if [ -n "$SONAR_TOKEN" ]; then \
         dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"; \
       fi

CMD ["dotnet", "test", "--no-restore"]
