using FluentAssertions;
using Moq;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Domain.Interfaces;
using Xunit;

namespace OficinaBilling.UnitTests.UseCases;

public class GerarOrcamentoUseCaseTests
{
    [Fact]
    public async Task Deve_falhar_quando_peca_nao_existe_no_cadastros()
    {
        // Arrange
        var orcamentoRepo = new Mock<IOrcamentoRepository>();
        var cadastrosClient = new Mock<ICadastrosServiceClient>();
        var eventPublisher = new Mock<IEventPublisher>();
        var logger = new Mock<IAppLogger<GerarOrcamentoUseCase>>();

        orcamentoRepo.Setup(r => r.ObterPorOrdemServicoIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Domain.Entities.Orcamento?)null);
        cadastrosClient.Setup(c => c.ObterPrecoPecaAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((PrecoItem?)null);

        var useCase = new GerarOrcamentoUseCase(orcamentoRepo.Object, cadastrosClient.Object, eventPublisher.Object, logger.Object);
        var request = new GerarOrcamentoRequest(Guid.NewGuid(), new List<ItemDiagnostico> { new(Guid.NewGuid(), 1) }, new List<ItemDiagnostico>());

        // Act
        var resultado = await useCase.ExecutarAsync(request);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Contain("não encontrada");
    }

    // TODO: caso feliz (soma peças + serviços corretamente, publica orcamento.gerado)
    // TODO: idempotência (já existe orçamento para a OS -> não duplica)
}
