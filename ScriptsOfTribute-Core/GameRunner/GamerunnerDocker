FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && \
    apt-get install -y rsync sudo wget apt-transport-https && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-7.0 aspnetcore-runtime-7.0 && \
    rm packages-microsoft-prod.deb

# Only copy the Linux build to reduce image size
COPY publish-linux/ /tot

WORKDIR /tot

ENTRYPOINT ["dotnet", "gamerunner.dll"]