# Multi-stage Docker build for Message Hub Server Light
# Stage 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY MessageHubServerLight.csproj .
RUN dotnet restore --runtime linux-x64

# Copy source code and build application
COPY . .
RUN dotnet build -c Release --no-restore
RUN dotnet publish -c Release -o /app/publish --no-build --runtime linux-x64 --self-contained false

# Stage 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Install required packages for SQLite and networking
RUN apk add --no-cache \
    sqlite \
    sqlite-dev \
    curl \
    tzdata

# Create non-root user for security
RUN addgroup -g 1000 messageHub && \
    adduser -u 1000 -G messageHub -s /bin/sh -D messageHub

# Set timezone to UTC
ENV TZ=UTC

# Copy published application
COPY --from=build /app/publish .

# Create directory for SQLite database with proper permissions
RUN mkdir -p /app/data && \
    chown -R messageHub:messageHub /app && \
    chmod +x MessageHubServerLight

# Switch to non-root user
USER messageHub

# Expose port
EXPOSE 8080

# Environment variables for ASP.NET Core
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/ping || exit 1

# Entry point
ENTRYPOINT ["./MessageHubServerLight"]