ConnectionStrings__BillingDb, MercadoPago__AccessToken e MercadoPago__WebhookSecret vêm do Secrets Manager, montados como Secret 'billing-secrets' (não versionado aqui).

MercadoPago__WebhookSecret = "assinatura secreta" exibida no painel do Mercado Pago (Suas integrações > Webhooks). Com MercadoPago__ValidateSignature=true (padrão), o webhook responde 401 sem ela.
