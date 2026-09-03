#!/usr/bin/env sh
set -eu

certificate=/etc/nginx/certs/tls.crt
certificate_key=/etc/nginx/certs/tls.key
server_name=${NGINX_SERVER_NAME:-localhost}

case "$server_name" in
  ''|*[!A-Za-z0-9.-]*)
    echo "NGINX_SERVER_NAME inválido." >&2
    exit 64
    ;;
esac

if [ ! -f "$certificate" ] || [ ! -f "$certificate_key" ]; then
  if [ "${NGINX_GENERATE_SELF_SIGNED_CERTIFICATE:-false}" != "true" ]; then
    echo "Certificado TLS ausente. Monte tls.crt/tls.key ou habilite o certificado local autogerado." >&2
    exit 1
  fi

  umask 077
  openssl req -x509 -nodes -newkey rsa:3072 -sha256 -days 30 \
    -keyout "$certificate_key" \
    -out "$certificate" \
    -subj "/CN=${NGINX_SERVER_NAME:-localhost}" \
    -addext "subjectAltName=DNS:${NGINX_SERVER_NAME:-localhost},IP:127.0.0.1" >/dev/null 2>&1
  chown nginx:nginx "$certificate" "$certificate_key"
fi

sed "s/__SERVER_NAME__/$server_name/g" /etc/nginx/nginx.conf > /tmp/nginx.conf

su-exec nginx test -r "$certificate"
su-exec nginx test -r "$certificate_key"
nginx -t -c /tmp/nginx.conf
exec nginx -c /tmp/nginx.conf -g "daemon off;"
