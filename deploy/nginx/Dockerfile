FROM nginx:1.30.4-alpine@sha256:02b1b2a0445514891a14aa371845f6085d5d9d10d385b30d6aad606a50a29a05

RUN apk add --no-cache openssl su-exec \
    && mkdir -p /etc/nginx/certs \
    && chown nginx:nginx /etc/nginx/certs

COPY nginx.conf /etc/nginx/nginx.conf
COPY proxy-common.conf /etc/nginx/proxy-common.conf
COPY docker-entrypoint.sh /usr/local/bin/sge-nginx-entrypoint

RUN chmod 0555 /usr/local/bin/sge-nginx-entrypoint

ENTRYPOINT ["/usr/local/bin/sge-nginx-entrypoint"]
