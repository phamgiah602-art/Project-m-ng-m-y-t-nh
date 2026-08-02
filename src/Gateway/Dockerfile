FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/Gateway/RemoteControlLAN.Gateway.csproj", "src/Gateway/"]
COPY ["src/Shared/RemoteControlLAN.Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/Gateway/RemoteControlLAN.Gateway.csproj"

# Copy full source code
COPY src/Gateway/ src/Gateway/
COPY src/Shared/ src/Shared/

WORKDIR "/src/src/Gateway"
RUN dotnet publish "RemoteControlLAN.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RemoteControlLAN.Gateway.dll"]
