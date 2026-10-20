FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /App

# Get the API content
COPY ./src/back/Veiller.Api/bin/Release/net10.0/publish/ ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "Veiller.Api.dll"]
