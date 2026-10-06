#!/usr/bin/env bash
set -euo pipefail

cd /liquibase

if [[ -x /liquibase/docker-entrypoint.sh ]]; then
  /liquibase/docker-entrypoint.sh validate
  /liquibase/docker-entrypoint.sh update
else
  liquibase validate
  liquibase update
fi
