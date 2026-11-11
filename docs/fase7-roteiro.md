# Fase 7 — Roteiro de execução e prints

Este roteiro gera **todas as evidências** pedidas na atividade da Fase 7. Cada
print tem um código (P01, P02...) que corresponde a um espaço reservado no
documento de entrega. Execute na ordem; tempo estimado: 1h30 na primeira vez.

> Pré-requisitos: Docker Desktop (Linux containers), Git, .NET SDK 10 (só para
> o certificado HTTPS) e **Wireshark** (https://www.wireshark.org/download.html).
> Todos os comandos são para PowerShell, na raiz do repositório.

Prints marcados com **(opcional)** reforçam o trabalho, mas podem ser pulados se
faltar tempo.

---

## Parte A — Preparação (uma vez)

```powershell
git pull
Copy-Item .env.example .env
notepad .env        # troque JWT_SECRET e RABBITMQ_PASSWORD por valores longos
.\scripts\generate-cert.ps1 -Password "local-only"
```

No `.env`, deixe `CERT_PASSWORD=local-only` (mesma senha do certificado).

---

## Parte B — Infraestrutura: rede, DHCP e DNS

Suba os microsserviços **com** a infraestrutura de rede:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml `
  -f docker-compose.infra.yml up --build -d
```

Aguarde ~30 s e tire os prints:

| Print | Comando / tela | O que deve aparecer |
|---|---|---|
| **P01** | `docker compose -f docker-compose.yml -f docker-compose.infra.yml ps` | 8 containers `Up`, incluindo `vinheria-dns-dhcp` e `vinheria-estacao-adega` |
| **P02** | `docker network inspect agnello-ms_vinheria --format "{{json .IPAM.Config}}"` e depois `docker network inspect agnello-ms_vinheria --format "{{range .Containers}}{{.Name}}  {{.IPv4Address}}{{println}}{{end}}"` | Sub-rede 10.20.0.0/24 e IP fixo de cada serviço (.2, .10 a .15) |
| **P03** | Abrir `infra/dnsmasq/dnsmasq.conf` no VS Code | Configuração da zona DNS e do escopo DHCP |
| **P04** | `docker logs vinheria-estacao-adega` | IP antes/depois do DHCP, lease recebido, nomes resolvidos e Gateway respondendo pelo nome |
| **P05** | `docker logs vinheria-dns-dhcp` | Linhas `DHCPDISCOVER`, `DHCPOFFER`, `DHCPREQUEST`, `DHCPACK` e as consultas DNS |
| **P06** | `docker exec vinheria-dns-dhcp cat /var/lib/misc/dnsmasq.leases` | Concessão ativa da `estacao-adega` (validade, MAC, IP, nome) |

> Observação para o P02: o Docker mostra para a `estacao-adega` o IP inicial da
> faixa dele (.64 a .127). O IP real, entregue pelo DHCP (.150 a .199), aparece
> no P04 e no P06. Essa diferença deve ser comentada no documento.

---

## Parte C — Segurança: HTTP sem criptografia (Wireshark)

Abra **dois** terminais.

**Terminal 1** — inicia a captura na rede do Gateway (40 s):

```powershell
.\scripts\capturar-trafego.ps1 -Arquivo http-sem-criptografia
```

**Terminal 2** — logo em seguida, executa o fluxo:

```powershell
.\scripts\demo-fluxo.ps1
```

| Print | Comando / tela | O que deve aparecer |
|---|---|---|
| **P16** | Saída do `demo-fluxo.ps1` | 1) 401 sem token; 2) token emitido; 3) estoque 200; 4) pedido criado; 5) 401 com token adulterado; payload do JWT decodificado |
| **P07** | `docker logs vinheria-dns-dhcp \| Select-String "estoque.vinheria.local"` | Consultas DNS vindas do Pedidos (10.20.0.13): os serviços se encontram pelo DNS interno |
| **P17** | Wireshark → abrir `capturas\http-sem-criptografia.pcap` → filtro `http` → clicar no `POST /v1/auth/token` → botão direito → **Follow → HTTP Stream** | **Usuário e senha em texto claro** |
| **P18** | Mesmo arquivo, Follow HTTP Stream em `POST /v1/estoque/reservas` | Header `Authorization: Bearer eyJ...` visível, ou seja, token que pode ser roubado e reutilizado |

---

## Parte D — Segurança: HTTPS (Wireshark)

Reinicie o ambiente no modo seguro:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml -f docker-compose.infra.yml down
docker compose -f docker-compose.yml -f docker-compose.build.yml `
  -f docker-compose.secure.yml -f docker-compose.infra.yml up --build -d
```

**Terminal 1:**

```powershell
.\scripts\capturar-trafego.ps1 -Filtro "tcp port 8443 or tcp port 8080" -Arquivo https-externo
```

**Terminal 2:**

```powershell
.\scripts\demo-fluxo.ps1 -Https
```

| Print | Comando / tela | O que deve aparecer |
|---|---|---|
| **P19** | Wireshark → `capturas\https-externo.pcap` → filtro `tls` | Client Hello / Server Hello e pacotes **Application Data** ilegíveis (porta 8443) |
| **P20** | Mesmo arquivo → Follow → TCP Stream em um pacote da 8443 | Conteúdo **cifrado**, sem senha nem token |
| **P21** (opcional) | Mesmo arquivo → filtro `http` | Tráfego **interno** Gateway → serviços (porta 8080) ainda em texto claro: argumento para mTLS |

> Se o Wireshark não reconhecer TLS na porta 8443: botão direito no pacote →
> **Decode As…** → TCP port 8443 → **TLS**.

Ao final: `docker compose -f docker-compose.yml -f docker-compose.infra.yml down`

---

## Parte E — Pipeline Jenkins

> O Jenkins baixa o código do GitHub. **Antes**, faça commit e push destes
> arquivos para o repositório do grupo.

```powershell
docker compose -f docker-compose.jenkins.yml up --build -d
docker exec vinheria-jenkins cat /var/jenkins_home/secrets/initialAdminPassword
```

1. Acesse http://localhost:8081, cole a senha, escolha **Install suggested plugins** e crie o usuário admin.
2. **Gerenciar Jenkins → Credentials → System → Global → Add Credentials**, tipo **Secret text**:
   `vinheria-jwt-secret`, `vinheria-rabbitmq-user`, `vinheria-rabbitmq-password`.
3. **Nova tarefa → Pipeline** (nome `vinheria-agnello`) → *Pipeline script from SCM* → Git →
   URL do repositório → branch `*/main` → Script Path `Jenkinsfile` → Salvar.
4. Clique em **Construir agora** (a primeira execução registra o parâmetro).
   Nas próximas, use **Construir com parâmetros** com `PUBLICAR_GHCR` desmarcado.

| Print | Comando / tela | O que deve aparecer |
|---|---|---|
| **P08** | Configuração do job (seção Pipeline) | SCM Git, URL do repositório, branch e `Jenkinsfile` |
| **P09** | Tela de Credentials | As 3 credenciais com IDs (valores ocultos) |
| **P10** | Página do build (visão de estágios) | Estágios verdes: Build simples → … → Deploy em pasta → CD: deploy → smoke test |
| **P11** | Console Output, início | `Deploy da Vinheria - build #N` |
| **P12** | Console Output, estágio *Deploy em pasta de publicacao* | `Publicado em .../releases/build-N`, `versao.txt` e listagem de `current` |
| **P13** | `docker exec vinheria-jenkins ls -la /var/jenkins_home/publicacao/vinheria /var/jenkins_home/publicacao/vinheria/current` | Pasta de publicação com `current -> releases/build-N` |
| **P14** | Console Output, estágio *DevSecOps* | Relatório do Trivy (segredos + vulnerabilidades HIGH/CRITICAL) |
| **P15** | Console Output, final | Smoke test OK e `Finished: SUCCESS` |

> Se o estágio DevSecOps ficar amarelo (UNSTABLE), o pipeline continua: a
> varredura é um relatório, não um bloqueio. Registre o resultado no documento.

Para desligar: `docker compose -f docker-compose.jenkins.yml down`

---

## Problemas comuns

| Sintoma | Causa provável | Solução |
|---|---|---|
| `estacao-adega` termina com "nenhum servidor DHCP respondeu" | `dns-dhcp` subiu depois | `docker restart vinheria-estacao-adega` |
| `exec ... no such file or directory` nos containers de infra | Arquivo `.sh` com quebra de linha CRLF | O `.gitattributes` evita isso; se persistir, `git rm --cached -r . ; git reset --hard` |
| Porta 5000 ocupada | Outro processo no Windows | `GATEWAY_PORT=5001` no `.env` |
| `Pool overlaps with other one on this address space` | Já existe rede Docker em 10.20.0.0/24 | `docker network prune` |
| Captura vazia | `demo-fluxo.ps1` rodou depois dos 40 s | Rode de novo, com o demo logo após iniciar a captura |
