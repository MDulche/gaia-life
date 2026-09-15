FROM mariadb:11.6

RUN apt-get update \
    && apt-get install -y --no-install-recommends cron tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY scripts/backup.sh /scripts/backup.sh
COPY docker/mariadb-backup/entrypoint.sh /entrypoint.sh
RUN chmod +x /scripts/backup.sh /entrypoint.sh \
    && mkdir -p /backups

WORKDIR /
ENV TZ=Europe/Paris
ENTRYPOINT ["/entrypoint.sh"]
