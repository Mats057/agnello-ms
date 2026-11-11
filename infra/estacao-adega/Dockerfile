# Simula uma estação de trabalho da adega que entra na rede e recebe
# IP, gateway, DNS e domínio do servidor DHCP (cliente DHCP do busybox).
FROM alpine:3.20

COPY udhcpc.sh entrypoint.sh /usr/local/bin/
RUN sed -i 's/\r$//' /usr/local/bin/udhcpc.sh /usr/local/bin/entrypoint.sh \
    && chmod +x /usr/local/bin/udhcpc.sh /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]