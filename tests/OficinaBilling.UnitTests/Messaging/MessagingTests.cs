using System.Text.Json;
using FluentAssertions;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Messages;
using OficinaBilling.Application.Messaging;
using OficinaBilling.Application.Messaging.Handlers;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Application.UseCases.Pagamento.CancelarPorOs;
using OficinaBilling.Application.UseCases.Pagamento.CriarCobranca;
using OficinaBilling.UnitTests.Support;

namespace OficinaBilling.UnitTests.Messaging;

public class MensagemEntranteTests
{
    [Theory]
    [InlineData("arn:aws:sns:us-east-1:123456789012:oficina-dev-diagnostico-concluido", "diagnostico.concluido")]
    [InlineData("arn:aws:sns:sa-east-1:1:oficina-prod-execucao-finalizada", "execucao.finalizada")]
    [InlineData("arn:aws:sns:us-east-1:1:oficina-homolog-os-cancelada", "os.cancelada")]
    public void Deriva_o_topico_do_arn(string arn, string esperado) =>
        MensagemEntrante.TopicoDoArn(arn).Should().Be(esperado);

    [Fact]
    public void Desembrulha_envelope_do_sns()
    {
        var os = Guid.NewGuid();
        var sns = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            TopicArn = "arn:aws:sns:us-east-1:1:oficina-dev-execucao-finalizada",
            Message = JsonSerializer.Serialize(new { ordemServicoId = os }),
            MessageAttributes = new { correlationId = new { Type = "String", Value = "corr-9" } }
        });

        var m = MensagemEntrante.Parse(sns);

        m.Topico.Should().Be("execucao.finalizada");
        m.CorrelationId.Should().Be("corr-9");
        JsonDocument.Parse(m.PayloadJson).RootElement.GetProperty("ordemServicoId").GetGuid().Should().Be(os);
    }

    [Fact]
    public void Aceita_envelope_de_dominio_com_dados()
    {
        var os = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new { tipo = "os.cancelada", correlationId = "c1", dados = new { ordemServicoId = os } });

        var m = MensagemEntrante.Parse(body);

        m.Topico.Should().Be("os.cancelada");
        m.CorrelationId.Should().Be("c1");
        JsonDocument.Parse(m.PayloadJson).RootElement.GetProperty("ordemServicoId").GetGuid().Should().Be(os);
    }

    [Fact]
    public void Aceita_envelope_padrao_do_grupo_e_normaliza_campos_de_diagnostico()
    {
        var os = Guid.NewGuid();
        var peca = Guid.NewGuid();
        var servico = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(),
            eventType = "diagnostico.concluido",
            eventVersion = "1.0",
            occurredAt = DateTime.UtcNow,
            correlationId = "trace-1",
            producer = "execucao-service",
            data = new
            {
                osId = os,
                pecas = new[] { new { pecaId = peca, quantidade = 2 } },
                servicos = new[] { new { servicoId = servico, quantidade = 1 } }
            }
        });

        var mensagem = MensagemEntrante.Parse(body);
        var dados = JsonDocument.Parse(mensagem.PayloadJson).RootElement;

        mensagem.Topico.Should().Be("diagnostico.concluido");
        mensagem.CorrelationId.Should().Be("trace-1");
        dados.GetProperty("ordemServicoId").GetGuid().Should().Be(os);
        dados.GetProperty("pecas")[0].GetProperty("id").GetGuid().Should().Be(peca);
        dados.GetProperty("servicos")[0].GetProperty("id").GetGuid().Should().Be(servico);
    }

    [Fact]
    public void Aceita_envelope_padrao_do_grupo_dentro_do_envelope_sns()
    {
        var os = Guid.NewGuid();
        var sns = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            TopicArn = "arn:aws:sns:us-east-1:1:oficina-dev-os-cancelada",
            Message = JsonSerializer.Serialize(new
            {
                eventType = "os.cancelada",
                data = new { osId = os, motivo = "cliente_desistiu" }
            })
        });

        var mensagem = MensagemEntrante.Parse(sns);

        mensagem.Topico.Should().Be("os.cancelada");
        JsonDocument.Parse(mensagem.PayloadJson).RootElement.GetProperty("ordemServicoId").GetGuid().Should().Be(os);
    }

    [Fact]
    public void Aceita_json_puro_com_campo_tipo()
    {
        var m = MensagemEntrante.Parse("{\"tipo\":\"os.cancelada\",\"ordemServicoId\":\"" + Guid.NewGuid() + "\"}");

        m.Topico.Should().Be("os.cancelada");
        m.PayloadJson.Should().Contain("ordemServicoId");
    }

    [Theory]
    [InlineData("isto-nao-e-json")]
    [InlineData("{\"semTipo\":true}")]
    [InlineData("[1,2,3]")]
    public void Mensagem_invalida_lanca_format_exception(string body) =>
        FluentActions.Invoking(() => MensagemEntrante.Parse(body)).Should().Throw<FormatException>();
}

public class HandlersTests
{
    private readonly InMemoryOrcamentoRepository _orcamentos = new();
    private readonly InMemoryPagamentoRepository _pagamentos = new();
    private readonly FakeOutbox _outbox = new();
    private readonly FakeCadastros _cadastros = new();
    private readonly FakeMercadoPago _mp = new();

    [Fact]
    public async Task Diagnostico_concluido_gera_orcamento()
    {
        var peca = _cadastros.AddPeca("Filtro", 40m);
        var os = Guid.NewGuid();
        var handler = new DiagnosticoConcluidoHandler(
            new GerarOrcamentoUseCase(_orcamentos, _cadastros, _outbox, new NullLogger<GerarOrcamentoUseCase>()));
        var payload = JsonSerializer.Serialize(new { ordemServicoId = os, pecas = new[] { new { id = peca, quantidade = 3 } }, servicos = Array.Empty<object>() });

        var r = await handler.ProcessarAsync(payload);

        handler.Topico.Should().Be("diagnostico.concluido");
        r.Sucesso.Should().BeTrue();
        (await _orcamentos.ObterPorOrdemServicoIdAsync(os))!.ValorTotal.Valor.Should().Be(120m);
    }

    [Fact]
    public async Task Execucao_finalizada_sem_orcamento_falha_para_reentrega()
    {
        var handler = new ExecucaoFinalizadaHandler(
            new CriarCobrancaUseCase(_orcamentos, _pagamentos, _mp, new NullLogger<CriarCobrancaUseCase>()));

        var r = await handler.ProcessarAsync(JsonSerializer.Serialize(new ExecucaoFinalizadaMessage(Guid.NewGuid())));

        handler.Topico.Should().Be("execucao.finalizada");
        r.Sucesso.Should().BeFalse();
        r.Tipo.Should().Be(TipoErro.NaoEncontrado);
    }

    [Fact]
    public async Task Os_cancelada_sem_nada_a_compensar_e_sucesso()
    {
        var handler = new OsCanceladaHandler(
            new CancelarPorOsUseCase(_orcamentos, _pagamentos, _mp, new NullLogger<CancelarPorOsUseCase>()));

        var r = await handler.ProcessarAsync(JsonSerializer.Serialize(new OsCanceladaMessage(Guid.NewGuid())));

        handler.Topico.Should().Be("os.cancelada");
        r.Sucesso.Should().BeTrue();
    }

    [Theory]
    [InlineData("{lixo")]
    [InlineData("null")]
    public async Task Payload_invalido_vira_falha_sem_excecao(string payload)
    {
        var handler = new OsCanceladaHandler(
            new CancelarPorOsUseCase(_orcamentos, _pagamentos, _mp, new NullLogger<CancelarPorOsUseCase>()));

        (await handler.ProcessarAsync(payload)).Sucesso.Should().BeFalse();
    }
}
