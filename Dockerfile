FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
ARG PROJECT=MicroRabbit.Banking.Api
RUN dotnet publish "Main Solution/MicroServicesRabbitMQ/${PROJECT}/${PROJECT}.csproj" -c Release -o /app --no-self-contained
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
# Compose sets the DLL explicitly so the same recipe builds either API.
ENTRYPOINT ["dotnet"]
