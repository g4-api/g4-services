# Use the official .NET 8 SDK image as the build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the build configuration (default is Release)
ARG BUILD_CONFIGURATION=Release

# Set the working directory inside the container
WORKDIR /src

# Copy the source files from src/ to /src
COPY ["src/", "/src"]

# Restore dependencies
RUN dotnet restore "G4.Services.Hub/G4.Services.Hub.csproj"

# Copy the remaining source code
COPY . .

# Build the project
RUN dotnet build "G4.Services.Hub/G4.Services.Hub.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the project
RUN dotnet publish "G4.Services.Hub/G4.Services.Hub.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# Use the official .NET 8 ASP.NET runtime as the runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set the working directory for the runtime
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose port 9944 (adjust if needed)
EXPOSE 9944

# Set the entry point for the container
ENTRYPOINT ["dotnet", "G4.Services.Hub.dll"]
