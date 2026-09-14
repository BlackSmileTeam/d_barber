FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Barber.Api/Barber.Api.csproj
RUN dotnet test Barber.Api.Tests/Barber.Api.Tests.csproj --no-restore --verbosity minimal
RUN dotnet publish Barber.Api/Barber.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:44315
EXPOSE 44315
ENTRYPOINT ["dotnet", "Barber.Api.dll"]
