FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://0.0.0.0:8080"]
