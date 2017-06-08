FROM frolvlad/alpine-mono

RUN apk add --no-cache unzip su-exec curl && \
    curl https://download.gtanet.work/server/latest.zip > /srv/gtanserver.zip && \
    cd /srv && \
    unzip gtanserver.zip && \
    rm gtanserver.zip

CMD ash -c '(cd /srv; mono GTANetworkServer.exe)'