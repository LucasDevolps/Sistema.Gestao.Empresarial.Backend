FROM nginx:1.28.0-alpine

RUN apk add --no-cache openssl su-exec \
    && mkdir -p /etc/nginx/certs \
    && chown nginx:nginx /etc/nginx/certs

COPY nginx.conf /etc/nginx/nginx.conf
COPY proxy-common.conf /etc/nginx/proxy-common.conf
COPY docker-entrypoint.sh /usr/local/bin/sge-nginx-entrypoint

RUN chmod 0555 /usr/local/bin/sge-nginx-entrypoint

ENTRYPOINT ["/usr/local/bin/sge-nginx-entrypoint"]
