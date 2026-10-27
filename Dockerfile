# Build context is the repo root (the build reads proto/users.proto):
#   docker build -f services/users-service/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo
COPY proto/ proto/
COPY services/users-service/src/UsersService/UsersService.csproj services/users-service/src/UsersService/
RUN dotnet restore services/users-service/src/UsersService/UsersService.csproj
COPY services/users-service/src/ services/users-service/src/
RUN dotnet publish services/users-service/src/UsersService/UsersService.csproj \
    -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
# The base image listens on 8080 by default; Program.cs binds GRPC_PORT instead.
ENV ASPNETCORE_HTTP_PORTS=
USER app
EXPOSE 50054
ENTRYPOINT ["dotnet", "UsersService.dll"]
