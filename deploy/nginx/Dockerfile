FROM nginx:1.31.8-alpine@sha256:785835ea65db4bba6f021acc0f3be91b0fef76a53fe41f8588c6698d2aed0606

# Require patched util-linux and expat libraries even when the base image is pinned.
RUN apk add --no-cache openssl su-exec 'libuuid>=2.42.3-r1' 'libexpat>=2.8.5-r0' \
    && mkdir -p /etc/nginx/certs \
    && chown nginx:nginx /etc/nginx/certs

COPY nginx.conf /etc/nginx/nginx.conf
COPY proxy-common.conf /etc/nginx/proxy-common.conf
COPY docker-entrypoint.sh /usr/local/bin/sge-nginx-entrypoint

RUN chmod 0555 /usr/local/bin/sge-nginx-entrypoint

ENTRYPOINT ["/usr/local/bin/sge-nginx-entrypoint"]
