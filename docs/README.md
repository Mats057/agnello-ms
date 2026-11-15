# Vinheria Agnello

Guia de instalação e execução do protótipo de microsserviços desenvolvido pela
equipe da Vinheria Agnello.

## 1. O que este projeto demonstra

Neste projeto, implementamos uma arquitetura de microsserviços para a Vinheria
Agnello:

- **Gateway**: única porta de entrada para clientes.
- **AuthService**: cadastro de usuários e emissão de tokens JWT.
- **PedidosService**: criação de pedidos e reserva de produtos.
- **EstoqueService**: consulta e reserva do estoque em memória.
- **MensageriaService**: publicação de eventos de negócio.
- **RabbitMQ**: broker usado para o evento `pedido.criado`.

O cliente acessa somente o Gateway. Os demais serviços conversam pela rede
interna do Docker Compose usando os nomes dos serviços como DNS. O fluxo
principal é:

```text
Cliente -> Gateway -> Auth/Pedidos/Estoque
                         |
                         v
                    Mensageria -> RabbitMQ
```

O estado dos usuários e do estoque é mantido em memória. Ao reiniciar os
containers, os dados de demonstração voltam ao estado inicial.

## 2. Pré-requisitos

Em uma máquina Windows, instale:

1. **Git**: https://git-scm.com/download/win
2. **Docker Desktop**: https://www.docker.com/products/docker-desktop/
3. **.NET SDK 10**: https://dotnet.microsoft.com/download/dotnet/10.0

O Docker Desktop precisa estar aberto e configurado para usar **Linux
containers**. O .NET SDK só é necessário para executar os testes diretamente
no Windows ou para gerar o certificado HTTPS local; a aplicação também pode
ser compilada inteiramente dentro do Docker.

Verifique a instalação no PowerShell:

```powershell
git --version
docker --version
docker compose version
dotnet --version
```

## 3. Baixar e configurar o projeto

Abra o PowerShell e execute:

```powershell
git clone https://github.com/Mats057/agnello-ms.git
Set-Location .\agnello-ms
```

Crie o arquivo local de configuração a partir do modelo:

```powershell
Copy-Item .env.example .env
```

Abra o arquivo para revisar os valores:

```powershell
notepad .env
```

Para executar o projeto, revise `JWT_SECRET` e `RABBITMQ_PASSWORD`. O arquivo
`.env` contém segredos locais e não deve ser commitado nem enviado ao GitHub.

As variáveis mais importantes são:

| Variável | Função |
|---|---|
| `JWT_SECRET` | Chave compartilhada para assinar e validar tokens |
| `RABBITMQ_USER` | Usuário do RabbitMQ |
| `RABBITMQ_PASSWORD` | Senha do RabbitMQ |
| `GATEWAY_PORT` | Porta do Gateway no computador host; padrão `5000` |
| `CERT_PASSWORD` | Senha do certificado HTTPS local |

As variáveis `ESTOQUE_URL`, `MENSAGERIA_URL` e `RABBITMQ_HOST` usam nomes DNS
internos do Compose. Não troque esses nomes por `localhost`: dentro de um
container, `localhost` aponta para o próprio container.

## 4. Executar localmente com Docker

O primeiro comando compila as cinco aplicações, cria os containers e inicia
RabbitMQ:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml up --build --remove-orphans
```

Deixe esse terminal aberto. Em outro PowerShell, confirme os containers:

```powershell
docker compose ps
```

O Gateway ficará disponível em:

- http://localhost:5000

Somente essa porta é publicada no host. As portas internas dos microsserviços
não ficam expostas diretamente, o que demonstra isolamento de rede.

Para parar a aplicação:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml down
```

Para parar e remover também os volumes do RabbitMQ:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml down --volumes
```

### Problemas comuns ao iniciar

- **Porta 5000 ocupada**: altere `GATEWAY_PORT=5001` no `.env` e acesse
  `http://localhost:5001`.
- **Docker não está rodando**: abra o Docker Desktop e espere o status
  “Running”.
- **Container encerrando**: veja os logs com
  `docker compose logs --tail=100 nome-do-servico`.
- **Alteração de código não aparece**: execute novamente o comando com
  `--build`.

## 5. Usar a documentação e testar a API

Abra http://localhost:5000/ no navegador. O Gateway apresenta o **Developer
Hub**, com o catálogo dos serviços, status e links para a documentação
interativa Scalar.

Links diretos:

- http://localhost:5000/auth/scalar
- http://localhost:5000/pedidos/scalar
- http://localhost:5000/estoque/scalar
- http://localhost:5000/mensageria/scalar

Também existem os atalhos `/docs/auth`, `/docs/pedidos`, `/docs/estoque` e
`/docs/mensageria`. O arquivo [`demo.http`](./demo.http) contém exemplos para
clientes HTTP compatíveis com o VS Code.

### Fluxo de demonstração pelo PowerShell

Para executar o fluxo de autenticação, utilizamos o usuário `demo` com a senha
`vinheria-demo`. Primeiro, solicite um token:

```powershell
$tokenResponse = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/auth/v1/auth/token `
  -ContentType 'application/json' `
  -Body '{"username":"demo","password":"vinheria-demo"}'

$token = $tokenResponse.access_token
$headers = @{ Authorization = "Bearer $token" }
```

Consulte o estoque:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:5000/estoque/v1/estoque `
  -Headers $headers
```

Crie um pedido:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/pedidos/v1/pedidos `
  -Headers $headers `
  -ContentType 'application/json' `
  -Body '{"sku":"vinho-tinto","quantity":2}'
```

Esse pedido faz três coisas: valida o JWT, solicita a reserva ao Estoque por
REST e publica `pedido.criado` através do RabbitMQ. Se a reserva não puder ser
feita, a API retorna `409`. Uma chamada protegida sem o header
`Authorization: Bearer <token>` retorna `401`.

Para cadastrar outro usuário:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/auth/v1/auth/register `
  -ContentType 'application/json' `
  -Body '{"username":"aluno","password":"senha123"}'
```

## 6. Executar testes

Os testes ficam em [`tests/ServiceTests`](../tests/ServiceTests). Com o .NET
SDK instalado:

```powershell
dotnet restore .\VinheriaAgnello.slnx
dotnet test .\VinheriaAgnello.slnx
```

Para compilar a solução sem executar testes:

```powershell
dotnet build .\VinheriaAgnello.slnx
```

O pipeline Jenkins também executa esses testes dentro da imagem
[`Dockerfile.ci`](../Dockerfile.ci).

## 7. HTTPS local (opcional)

O HTTP é suficiente para executar o projeto. Para demonstrar comunicação
criptografada e comparar HTTP/HTTPS no Wireshark, gere um certificado local:

```powershell
.\scripts\generate-cert.ps1 -Password "local-only"
```

Se o PowerShell bloquear a execução do script nesta sessão:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\generate-cert.ps1 -Password "local-only"
```

Inicie a aplicação com o Compose seguro:

```powershell
docker compose `
  -f docker-compose.yml `
  -f docker-compose.build.yml `
  -f docker-compose.secure.yml `
  up --build --remove-orphans
```

Acesse:

- https://localhost:5440

Como o certificado é autoassinado, o navegador pode mostrar um aviso de
confiança. O tráfego HTTP permite observar o conteúdo do payload; no HTTPS o
conteúdo é criptografado, embora metadados como IP, porta e tamanho continuem
visíveis.

## 8. CI/CD com Jenkins e GHCR

O [`Jenkinsfile`](../Jenkinsfile) implementa o fluxo:

1. checkout do repositório;
2. criação temporária do `.env`;
3. validação dos arquivos Compose;
4. build e testes na imagem CI;
5. build das cinco imagens;
6. push para o pacote único no GitHub Container Registry;
7. pull das imagens publicadas;
8. deploy com `--no-build`;
9. smoke test do Gateway.

As imagens usam este formato:

```text
ghcr.io/mats057/agnello-ms:<número-do-build>-<serviço>
```

Exemplo: `ghcr.io/mats057/agnello-ms:12-pedidos-service`.

### 8.1 Iniciar o Jenkins

O Compose do Jenkins cria um container Jenkins e um daemon Docker-in-Docker
isolado. Na raiz do projeto:

```powershell
docker compose -f docker-compose.jenkins.yml up --build -d
docker logs -f vinheria-jenkins
```

Abra http://localhost:8081. Para obter a senha inicial:

```powershell
docker exec vinheria-jenkins `
  cat /var/jenkins_home/secrets/initialAdminPassword
```

Instale os plugins sugeridos pelo Jenkins, principalmente **Pipeline** e
**Git**, e crie um job do tipo **Pipeline**.

Na configuração do job:

1. selecione **Pipeline script from SCM**;
2. escolha **Git**;
3. informe `https://github.com/Mats057/agnello-ms.git`;
4. use a branch `*/main`;
5. indique `Jenkinsfile` como script;
6. salve e clique em **Build Now**.

### 8.2 Credenciais do Jenkins

Crie três credenciais do tipo **Secret text**:

| ID | Valor |
|---|---|
| `vinheria-jwt-secret` | uma chave JWT longa e aleatória |
| `vinheria-rabbitmq-user` | por exemplo, `vinheria` |
| `vinheria-rabbitmq-password` | uma senha do RabbitMQ |

Para o GHCR, crie também uma credencial **Username with password**:

- **ID**: `github-container-registry`
- **Username**: seu usuário GitHub, por exemplo `Mats057`
- **Password**: PAT classic com `write:packages` e `read:packages`

Nunca coloque esse token no código, no `.env` ou no Jenkinsfile. O pacote
`agnello-ms` precisa existir no GHCR e a conta usada no Jenkins deve ter
permissão para publicar nele.

### 8.3 Acompanhar e limpar o Jenkins

No job, use **Console Output** para acompanhar cada etapa. O deploy ocorre no
daemon Docker-in-Docker criado pelo Compose do Jenkins, não diretamente no
Docker Desktop do host.

Para parar o ambiente:

```powershell
docker compose -f docker-compose.jenkins.yml down
```

Para remover também os dados persistidos do Jenkins:

```powershell
docker compose -f docker-compose.jenkins.yml down --volumes
```

## 9. Arquivos importantes

- [`docker-compose.yml`](../docker-compose.yml): serviços e deploy.
- [`docker-compose.build.yml`](../docker-compose.build.yml): instruções de
  build das imagens.
- [`docker-compose.secure.yml`](../docker-compose.secure.yml): configuração
  HTTPS local.
- [`docker-compose.jenkins.yml`](../docker-compose.jenkins.yml): Jenkins e
  Docker-in-Docker.
- [`Jenkinsfile`](../Jenkinsfile): pipeline CI/CD.
- [`docs/arquitetura.md`](./arquitetura.md): visão resumida da arquitetura.
- [`docs/demo.http`](./demo.http): exemplos de chamadas HTTP.

## 9.1. Fase 7: rede, DHCP/DNS, deploy e segurança

Na Fase 7, implementamos:

- [`docker-compose.infra.yml`](../docker-compose.infra.yml): rede `10.20.0.0/24`
  com IPs fixos para os serviços, servidor **DHCP + DNS** (dnsmasq, zona
  `vinheria.local`) e uma estação cliente que recebe IP por DHCP;
- [`infra/`](../infra): imagens do servidor DHCP/DNS e da estação da adega;
- [`Jenkinsfile`](../Jenkinsfile): estágios `Build simples`, `DevSecOps`
  (Trivy) e `Deploy em pasta de publicacao` (simula IIS); o envio ao GHCR
  passou a ser opcional pelo parâmetro `PUBLICAR_GHCR`;
- [`scripts/demo-fluxo.ps1`](../scripts/demo-fluxo.ps1) e
  [`scripts/capturar-trafego.ps1`](../scripts/capturar-trafego.ps1): fluxo de
  autenticação e captura de tráfego para o Wireshark.

O passo a passo completo, com a lista de prints da entrega, está em
[`docs/fase7-roteiro.md`](./fase7-roteiro.md).
