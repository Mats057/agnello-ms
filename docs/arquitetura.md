# Arquitetura simplificada

```text
Cliente / PowerShell
        |
        | HTTP :5000 ou HTTPS :5440
        v
   gateway-service
   /auth /pedidos /estoque /mensageria
        |
        +---------------- vinheria-network ----------------------+
        |             |                 |                        |
   auth-service  pedidos-service  estoque-service       mensageria-service
                                                        |
                                                        | AMQP
                                                        v
                                                     rabbitmq
```

O fluxo de compra é:

1. O cliente acessa apenas o Gateway.
2. O cliente cadastra-se ou obtém um JWT em `/auth/v1/auth`.
3. O Gateway encaminha a chamada para `/pedidos` ou `/estoque`.
4. Pedidos valida o JWT e chama Estoque por REST interno.
5. Pedidos chama Mensageria, que publica `pedido.criado` no RabbitMQ.
6. O pedido fica `CONFIRMADO` ou `CONFIRMADO_EVENTO_PENDENTE`.

O Gateway é a única aplicação com porta publicada. O DNS interno do Compose
resolve os nomes dos serviços e do broker, simulando descoberta de serviços e
isolamento de rede. Em produção, o HMAC local seria substituído por
Keycloak/OIDC, o estado seria persistido em banco e RabbitMQ poderia ser
Kafka ou Azure Service Bus.
