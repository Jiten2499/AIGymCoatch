FROM liquibase/liquibase:4.31

USER root

COPY changelog/ /liquibase/changelog/
COPY procedures/ /liquibase/procedures/
COPY views/ /liquibase/views/
COPY functions/ /liquibase/functions/
COPY seed/ /liquibase/seed/
COPY migrate.sh /liquibase/migrate.sh

RUN chmod 755 /liquibase/migrate.sh && sed -i 's/\r$//' /liquibase/migrate.sh

USER liquibase

ENV LIQUIBASE_COMMAND_CHANGELOG_FILE=changelog/db.changelog-master.yaml

ENTRYPOINT ["/liquibase/migrate.sh"]
