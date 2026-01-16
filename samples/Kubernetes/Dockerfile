# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Veggerby.Ignition.sln", "./"]
COPY ["src/Veggerby.Ignition/Veggerby.Ignition.csproj", "src/Veggerby.Ignition/"]
COPY ["samples/Kubernetes/Kubernetes.csproj", "samples/Kubernetes/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

# Restore dependencies
RUN dotnet restore "samples/Kubernetes/Kubernetes.csproj"

# Copy source code
COPY ["src/Veggerby.Ignition/", "src/Veggerby.Ignition/"]
COPY ["samples/Kubernetes/", "samples/Kubernetes/"]

# Build application
WORKDIR "/src/samples/Kubernetes"
RUN dotnet build "Kubernetes.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Kubernetes.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=publish /app/publish .

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD curl --fail http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Kubernetes.dll"]
