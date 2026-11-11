#!/bin/sh
# Script chamado pelo cliente DHCP (busybox udhcpc) a cada evento do lease.
# Aplica o IP, a rota e o DNS recebidos do servidor DHCP.

case "$1" in
  deconfig)
    ip -4 addr flush dev "$interface"
    ;;
  bound|renew)
    ip -4 addr flush dev "$interface"
    ip -4 addr add "$ip/${mask:-24}" dev "$interface"
    [ -n "$router" ] && ip -4 route replace default via "$router" dev "$interface"
    {
      [ -n "$domain" ] && echo "search $domain"
      for server in $dns; do echo "nameserver $server"; done
    } > /etc/resolv.conf
    echo "[DHCP] lease recebido: ip=$ip mascara=$mask gateway=$router dns=$dns dominio=$domain servidor=$serverid"
    ;;
esac
exit 0