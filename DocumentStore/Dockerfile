#
# Phase 1: Build the dotnet-core application by rendering the DLL's of this project
#
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build

ARG TARGETARCH
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1


WORKDIR /app

RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/* /usr/share/man/?? /usr/share/man/??_*

# Copy csproj and restore as distinct layers
COPY ["DocumentStore/DocumentStore.csproj", "DocumentStore/"]
COPY ["GrpcServices/GrpcServices.csproj", "GrpcServices/"]
COPY **/*.sln ./
RUN dotnet restore DocumentStore/DocumentStore.csproj

# Copy the remaining files and build the application
COPY . ./

# - Build the application
RUN dotnet publish -a $TARGETARCH -c Release -f net9.0 --property:PublishDir=/app/out

#
# Phase 2: Render the final image that only contains the runtime and the DLL's that we have compiled in phase 1 above
#
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime

# - Add aspnet user and group with GUID 1000
RUN groupadd -r -g 1000 aspnet && \
    useradd -r -d /home/aspnet -s /bin/bash -G aspnet -g aspnet -u 1000 aspnet && \
    mkdir -p /home/aspnet && \
    chown -R aspnet:aspnet /home/aspnet

# - Add Taxxor root certificate
COPY --chown=aspnet:aspnet ./DocumentStore/Taxxor_root.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates 2>/dev/null

# - Install additional packages in the image that we need to run the application
# a) setup workdir
RUN mkdir -p /root/work/
WORKDIR /root/work/

# b) install additional packages
RUN apt-get -y update \
    && apt-get upgrade -y \
    && apt-get -y install git nano curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/* /usr/share/man/?? /usr/share/man/??_*

# c) slim down image
RUN apt-get clean && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/* /usr/share/man/?? /usr/share/man/??_*

# - Move the generated DLL's to the /app directory
RUN mkdir -p /app/DocumentStore/
WORKDIR /app/DocumentStore/
COPY --chown=aspnet:aspnet --from=build /app/out ./

# - Copy all other files needed for this application (note that you can use .dockerignore for limiting the files that will be copied into the image)
COPY --chown=aspnet:aspnet . /app/

# Assure that we can map a directories from the host system
RUN rm -rf /app/DocumentStore/data

# - Remove the source files from the image we deploy
RUN find /app/DocumentStore/ -type f -name "*.cs" -delete

# - Remove the empty directories that this leaves behind
RUN find /app/DocumentStore/backend -type d -empty -delete

# - Remove the Taxxor root certificate from the image
RUN rm -rf /app/DocumentStore/Taxxor_root.crt

# - Expose the webserver port that we will be using to access the application
EXPOSE 4813

# - Opt out of the MS telemetry function
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# - Copy the startup script to the docker image
COPY ./entrypoint*.sh /app/
RUN chmod +x /app/*.sh && chown aspnet:aspnet /app/*.sh

# - Run the main process with the special user we created
USER aspnet:aspnet

# Script that launches the app
ENTRYPOINT ["/app/entrypoint.sh"]