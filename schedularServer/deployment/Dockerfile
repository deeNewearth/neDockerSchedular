FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
MAINTAINER shree dee <dee@labizbille.com>

EXPOSE 80
ENV ASPNETCORE_URLS http://*:80


WORKDIR /app

COPY ./app ./

ENTRYPOINT ["dotnet", "schedularServer.dll"]


