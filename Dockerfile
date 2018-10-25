FROM microsoft/dotnet:2.1.403-sdk-alpine3.7 AS build
WORKDIR /app

ADD . .
RUN dotnet restore && \
    dotnet publish -c Release -o out

FROM microsoft/dotnet:2.1.5-aspnetcore-runtime-alpine3.7 AS runtime
WORKDIR /app
COPY --from=build /app/MinChain/out .

ENTRYPOINT [ "./run.sh" ]
