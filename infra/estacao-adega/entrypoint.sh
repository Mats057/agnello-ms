#!/bin/sh
# Estação da adega: pede IP via DHCP, testa o DNS interno e acessa o Gateway
# pelo nome. A saída deste container é a evidência do DHCP + DNS funcionando.

linha() { echo "------------------------------------------------------------"; }

linha
echo "ESTACAO DA ADEGA - cliente DHCP da rede vinheria.local"
linha
echo "IP antes do DHCP (atribuido pelo Docker):"
ip -4 -o addr show eth0 | awk '{print "  " $4}'

echo
echo "Solicitando IP ao servidor DHCP (DISCOVER -> OFFER -> REQUEST -> ACK)..."
if ! udhcpc -i eth0 -x hostname:estacao-adega -s /usr/local/bin/udhcpc.sh -q -n -t 10 -T 2; then
  echo "ERRO: nenhum servidor DHCP respondeu."
  exit 1
fi

echo
echo "IP depois do DHCP:"
ip -4 -o addr show eth0 | awk '{print "  " $4}'
echo "Rota padrao: $(ip route | awk '/default/ {print $3}')"
echo "/etc/resolv.conf:"
sed 's/^/  /' /etc/resolv.conf

linha
echo "Resolucao de nomes no DNS interno (10.20.0.2)"
linha
for host in gateway auth pedidos estoque mensageria api estacao-adega; do
  ip_resolvido="$(nslookup "$host.vinheria.local" 2>/dev/null | awk '/^Address/ && !/#53/ && !/:53$/ {print $2}' | tail -n 1)"
  printf '  %-30s -> %s\n' "$host.vinheria.local" "${ip_resolvido:-NAO RESOLVIDO}"
done

linha
echo "Acesso ao Gateway pelo nome DNS"
linha
for tentativa in $(seq 1 20); do
  if resposta="$(wget -qO- http://gateway.vinheria.local:8080/health 2>/dev/null)"; then
    echo "  GET http://gateway.vinheria.local:8080/health -> $resposta"
    break
  fi
  sleep 2
done
[ -z "${resposta:-}" ] && echo "  Gateway ainda nao respondeu."

linha
echo "Estacao pronta. Mantendo o container ativo para testes (docker exec)."
exec sleep infinity