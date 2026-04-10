# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ["AutoFixer.slnx", "./"]
COPY ["src/AutoFixer/AutoFixer.csproj", "src/AutoFixer/"]
COPY ["tests/AutoFixer.Tests/AutoFixer.Tests.csproj", "tests/AutoFixer.Tests/"]

# Restore dependencies
RUN dotnet restore "src/AutoFixer/AutoFixer.csproj"

# Copy the rest of the source code
COPY . .

# Build and Publish
WORKDIR "/src/src/AutoFixer"
RUN dotnet publish "AutoFixer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
WORKDIR /app

# Create a non-root user for security (optional but recommended)
# RUN addgroup -g 1000 appgroup && adduser -u 1000 -G appgroup -D appuser
# USER appuser

COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "AutoFixer.dll"]
