# language: pt
Funcionalidade: Orçamento e pagamento da ordem de serviço
  Como oficina, quero gerar o orçamento a partir do diagnóstico, receber a aprovação do cliente
  e cobrar pelo Mercado Pago, publicando os eventos que coordenam o Saga entre os serviços.

  Cenário: Diagnóstico concluído gera orçamento
    Dado uma ordem de serviço com diagnóstico de 2 peças de 50 reais e 1 serviço de 100 reais
    Então o orçamento da OS está "Gerado" com valor total de 200 reais
    E o evento "orcamento.gerado" foi publicado

  Cenário: Cliente aprova orçamento e o evento é publicado
    Dado uma ordem de serviço com diagnóstico de 2 peças de 50 reais e 1 serviço de 100 reais
    Quando o cliente aprova o orçamento
    Então o orçamento da OS está "Aprovado" com valor total de 200 reais
    E o evento "orcamento.aprovado" foi publicado

  Cenário: Cliente recusa o orçamento e o Saga é acionado
    Dado uma ordem de serviço com diagnóstico de 1 peças de 80 reais e 1 serviço de 20 reais
    Quando o cliente recusa o orçamento
    Então o evento "orcamento.recusado" foi publicado

  Cenário: Webhook de pagamento confirmado dispara evento
    Dado uma ordem de serviço com diagnóstico de 2 peças de 50 reais e 1 serviço de 100 reais
    E o cliente aprovou o orçamento
    E a execução da OS foi finalizada
    Quando o Mercado Pago notifica o pagamento como "approved"
    Então o pagamento da OS está "Confirmado"
    E o evento "pagamento.confirmado" foi publicado

  Cenário: Webhook de pagamento recusado dispara evento de compensação
    Dado uma ordem de serviço com diagnóstico de 2 peças de 50 reais e 1 serviço de 100 reais
    E o cliente aprovou o orçamento
    E a execução da OS foi finalizada
    Quando o Mercado Pago notifica o pagamento como "rejected"
    Então o pagamento da OS está "Recusado"
    E o evento "pagamento.recusado" foi publicado

  Cenário: Notificação duplicada do Mercado Pago não duplica o evento
    Dado uma ordem de serviço com diagnóstico de 2 peças de 50 reais e 1 serviço de 100 reais
    E o cliente aprovou o orçamento
    E a execução da OS foi finalizada
    Quando o Mercado Pago notifica o pagamento como "approved"
    E o Mercado Pago notifica o pagamento como "approved"
    Então o evento "pagamento.confirmado" foi publicado 1 vez

  Cenário: Cancelamento da OS compensa orçamento e cobrança
    Dado uma ordem de serviço com diagnóstico de 2 peças de 50 reais e 1 serviço de 100 reais
    E o cliente aprovou o orçamento
    E a execução da OS foi finalizada
    Quando a OS é cancelada
    Então o orçamento da OS está "Cancelado" com valor total de 200 reais
    E o pagamento da OS está "Cancelado"
