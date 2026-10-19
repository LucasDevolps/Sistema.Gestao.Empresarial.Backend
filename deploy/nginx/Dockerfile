FROM nginx:1.31.7-alpine@sha256:892bb2152464f0c5d8972593a5f5afaf8054552580b699d95f8ff4af08c2d33d

# Require patched util-linux and expat libraries even when the base image is pinned.
RUN apk add --no-cache openssl su-exec 'libuuid>=2.42.3-r1' 'libexpat>=2.8.5-r0' \
    && mkdir -p /etc/nginx/certs \
    && chown nginx:nginx /etc/nginx/certs

COPY nginx.conf /etc/nginx/nginx.conf
COPY proxy-common.conf /etc/nginx/proxy-common.conf
COPY docker-entrypoint.sh /usr/local/bin/sge-nginx-entrypoint

RUN chmod 0555 /usr/local/bin/sge-nginx-entrypoint

ENTRYPOINT ["/usr/local/bin/sge-nginx-entrypoint"]
