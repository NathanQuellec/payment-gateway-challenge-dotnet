FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/PaymentGateway.Api/PaymentGateway.Api.csproj src/PaymentGateway.Api/
# The container uses the serviced .NET 8 SDK; global.json pins local development.
RUN dotnet restore src/PaymentGateway.Api/PaymentGateway.Api.csproj
COPY src/PaymentGateway.Api/ src/PaymentGateway.Api/
RUN dotnet publish src/PaymentGateway.Api/PaymentGateway.Api.csproj -c Release --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Hosting__UseHttpsRedirection=false
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
