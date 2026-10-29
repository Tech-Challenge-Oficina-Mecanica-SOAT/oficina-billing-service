using FluentAssertions;
using OficinaBilling.Application.Security;
using Xunit;

namespace OficinaBilling.UnitTests.Security;

public class MercadoPagoWebhookSignatureTests
{
    private const string Segredo = "segredo-de-teste";

    private static string Header(string dataId, string requestId, string ts = "1704908010", string segredo = Segredo) =>
        $"ts={ts},v1={MercadoPagoWebhookSignature.CalcularAssinatura(segredo, dataId, requestId, ts)}";

    [Fact]
    public void Assinatura_correta_e_aceita()
    {
        var header = Header("123456", "req-abc");
        MercadoPagoWebhookSignature.Validar(Segredo, "123456", "req-abc", header).Should().BeTrue();
    }

    [Fact]
    public void Segredo_errado_e_rejeitado()
    {
        var header = Header("123456", "req-abc", segredo: "outro-segredo");
        MercadoPagoWebhookSignature.Validar(Segredo, "123456", "req-abc", header).Should().BeFalse();
    }

    [Fact]
    public void Data_id_adulterado_e_rejeitado()
    {
        var header = Header("123456", "req-abc");
        MercadoPagoWebhookSignature.Validar(Segredo, "999999", "req-abc", header).Should().BeFalse();
    }

    [Fact]
    public void Request_id_adulterado_e_rejeitado()
    {
        var header = Header("123456", "req-abc");
        MercadoPagoWebhookSignature.Validar(Segredo, "123456", "req-outro", header).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("ts=1704908010")]
    [InlineData("v1=abc")]
    public void Header_ausente_ou_malformado_e_rejeitado(string? header)
    {
        MercadoPagoWebhookSignature.Validar(Segredo, "123456", "req-abc", header).Should().BeFalse();
    }

    [Fact]
    public void Segredo_vazio_rejeita_sempre()
    {
        var header = Header("123456", "req-abc", segredo: "");
        MercadoPagoWebhookSignature.Validar("", "123456", "req-abc", header).Should().BeFalse();
    }

    [Fact]
    public void Data_id_alfanumerico_e_normalizado_para_minusculas()
    {
        // Regra do MP: ids alfanuméricos entram em minúsculas no texto assinado.
        var header = Header("ABC123", "req-abc");
        MercadoPagoWebhookSignature.Validar(Segredo, "ABC123", "req-abc", header).Should().BeTrue();
        MercadoPagoWebhookSignature.Validar(Segredo, "abc123", "req-abc", header).Should().BeTrue();
    }
}
