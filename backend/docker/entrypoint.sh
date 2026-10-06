#!/bin/sh
set -eu
mkdir -p /var/lifefit/dataprotection
chown -R app:app /var/lifefit/dataprotection
exec su -s /bin/sh app -c 'exec dotnet LifeFitAI.Api.dll'
