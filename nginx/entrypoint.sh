#!/bin/sh
set -eu

: "${DOMAIN:?DOMAIN must be set}"
envsubst '${DOMAIN}' < /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf

exec "$@"
