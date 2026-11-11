# Servidor DHCP + DNS da rede interna da Vinheria (equivalente às funções
# "DHCP Server" e "DNS Server" do Windows Server).
FROM alpine:3.20

RUN apk add --no-cache dnsmasq \
    && mkdir -p /var/lib/misc \
    && chown dnsmasq:dnsmasq /var/lib/misc

COPY dnsmasq.conf /etc/dnsmasq.conf
# Remove CRLF caso o arquivo tenha vindo de um checkout no Windows.
RUN sed -i 's/\r$//' /etc/dnsmasq.conf && dnsmasq --test --conf-file=/etc/dnsmasq.conf

EXPOSE 53/udp 53/tcp 67/udp
ENTRYPOINT ["dnsmasq", "--keep-in-foreground", "--log-facility=-", "--conf-file=/etc/dnsmasq.conf"]