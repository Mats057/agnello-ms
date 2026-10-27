# Protótipo da Vinheria Agnello

Este protótipo simula uma arquitetura de microsserviços com:

- **Gateway**: único ponto de entrada público.
- **Auth**: cadastro de usuário e emissão de JWT.
- **MS-05 Estoque**: saldos em memória e reservas.
- **MS-06 Pedidos**: criação de pedidos e chamada REST ao Estoque.
- **Mensageria**: publicação de eventos em RabbitMQ.

## Pré-requisitos

- Docker Desktop com Compose habilitado.
- .NET SDK 10 para build/testes diretamente no Windows.

## Execução local

```powershell
docker compose up --build
```

Somente o Gateway publica uma porta:

- `http://localhost:5000` — Gateway.
- `http://localhost:15672` — console opcional do RabbitMQ (`vinheria`/`vinheria`).

Os microsserviços não possuem portas publicadas no host. Use os prefixos do
Gateway:

- `/auth/...`
- `/pedidos/...`
- `/estoque/...`
- `/mensageria/...`

Ao abrir `http://localhost:5000/` (ou `https://localhost:5440/` no perfil
seguro), você encontra o **Developer Hub** do Gateway. Ele reúne o catálogo
das APIs, links para Scalar/OpenAPI, fluxo de integração, exemplos de payload
e o status dos serviços. Esse é o ponto de entrada recomendado para uma
aplicação cliente que não conhece a topologia interna.

As documentações usam OpenAPI nativo e Scalar:

- `http://localhost:5000/auth/scalar`
- `http://localhost:5000/pedidos/scalar`
- `http://localhost:5000/estoque/scalar`
- `http://localhost:5000/mensageria/scalar`

Também existem redirecionamentos amigáveis em `/docs/auth`,
`/docs/pedidos`, `/docs/estoque` e `/docs/mensageria`.

## Roteiro de demonstração

O usuário inicial de laboratório é `demo` com senha `vinheria-demo`.

O AuthService é o único serviço que cria usuários e emite tokens. Pedidos e
Estoque não mantêm usuários nem emitem tokens: eles validam a assinatura
HMAC-SHA256, expiração e role `user` do Bearer token emitido pelo AuthService.
O mesmo `JWT_SECRET` é injetado nos três containers pelo `.env`, sem repetir a
chave no `docker-compose.yml`. Para configurar uma nova máquina, copie
`.env.example` para `.env` e substitua os valores locais.
As credenciais do RabbitMQ também ficam somente no `.env`.

```powershell
$token = (Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/auth/v1/auth/token `
  -ContentType 'application/json' `
  -Body '{"username":"demo","password":"vinheria-demo"}').access_token

$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri http://localhost:5000/estoque/v1/estoque -Headers $headers

Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/pedidos/v1/pedidos `
  -Headers $headers `
  -ContentType 'application/json' `
  -Body '{"sku":"vinho-tinto","quantity":2}'
```

Para cadastrar outro usuário:

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://localhost:5000/auth/v1/auth/register `
  -ContentType 'application/json' `
  -Body '{"username":"aluno","password":"senha123"}'
```

Uma chamada protegida sem `Authorization` retorna `401`. Uma quantidade maior
que o saldo retorna `409`, simulando falha da reserva.

Depois de criar um pedido, Pedidos publica o evento `pedido.criado` através do
serviço de Mensageria no exchange `vinheria.events`. Se o broker estiver
indisponível, o pedido fica explicitamente como
`CONFIRMADO_EVENTO_PENDENTE`.

## Isolamento e DNS

Os serviços compartilham a rede `vinheria-network`, mas apenas o Gateway pode
ser acessado pelo host. O Compose fornece DNS interno para
`auth-service`, `pedidos-service`, `estoque-service`, `mensageria-service` e
`rabbitmq`. Isso simula descoberta e registro de serviços sem expor as portas
internas.

## HTTPS local

Para gerar um certificado de laboratório:

```powershell
.\scripts\generate-cert.ps1
docker compose -f docker-compose.yml -f docker-compose.secure.yml up --build
```

O Gateway HTTPS usa `https://localhost:5440`. Como o certificado é local, o
navegador ou PowerShell pode pedir uma exceção de confiança. Use as mesmas
rotas, por exemplo `https://localhost:5440/pedidos/scalar`.

Nunca use o certificado, o segredo JWT ou as credenciais do RabbitMQ deste
protótipo em produção.

## Jenkins

O arquivo [`Jenkinsfile`](../Jenkinsfile) executa checkout, validação do
Compose, build/teste dentro de uma imagem CI e deploy real da stack com
`docker compose up`. Depois executa smoke tests contra o Gateway. A imagem Jenkins está em
[`jenkins/Dockerfile`](../jenkins/Dockerfile).

```powershell
docker compose -f docker-compose.jenkins.yml up --build -d
docker logs vinheria-jenkins
```

Abra [http://localhost:8081](http://localhost:8081), informe a senha inicial e
crie um job Pipeline usando **Pipeline script from SCM**. Depois clique em
**Build Now** e acompanhe **Console Output**, **Stage View**, **Workspace** e
**Artifacts**.

Antes do primeiro build, crie no Jenkins as credenciais secret text:

- `vinheria-jwt-secret`;
- `vinheria-rabbitmq-user`;
- `vinheria-rabbitmq-password`.

O pipeline injeta esses valores somente durante o deploy, cria um `.env`
temporário no workspace e não os grava no Git. O Compose do Jenkins inicia um daemon Docker-in-Docker dedicado, usado pelo
pipeline para gerar imagens e executar o deploy isolado. Isso evita depender
de pipes do Docker Desktop do Windows e permite reproduzir o build no agente.
O deploy desse ambiente fica dentro do daemon CI; para publicar em homologação
ou produção, substitua esse daemon por um agente Jenkins dedicado ou por um
registry/servidor de deploy configurado pela equipe.

## Limitações acadêmicas

O JWT usa HMAC-SHA256 e usuários em memória. Em produção, use Keycloak/OIDC,
chaves assimétricas, armazenamento persistente e rotação de segredos. RabbitMQ
é usado para demonstrar mensageria; Kafka ou Azure Service Bus seriam
alternativas para uma implantação real.

Para a evidência do Wireshark, compare HTTP e HTTPS com dados fictícios: no
HTTP o payload pode ser lido; no HTTPS apenas metadados como IP, porta e
tamanho ficam visíveis.
