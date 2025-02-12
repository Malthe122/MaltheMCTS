FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

# Copy the self-contained GameRunner and all necessary files
COPY publish-linux/ /tot

RUN chmod -R +x /tot

WORKDIR /tot

# Default to bash so the container stays open
CMD [ "bash" ]

