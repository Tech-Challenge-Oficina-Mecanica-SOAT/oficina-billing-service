using FluentAssertions;
using Moq;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Orcamento;
using OficinaBilling.Domain.Interfaces;
using Xunit;

namespace OficinaBilling.UnitTests.UseCases;

public class GerarOrcamentoUseCaseTests
{
    private static GerarOrcamentoUseCase CriarUseCase(
        Mock<IOrcamentoRepository> orcamentoRepo,
        Mock<ICadastrosServiceClient> cadastrosClient,
        Mock<IEventPublisher>? eventPublisher = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        return new GerarOrcamentoUseCase(
            orcamentoRepo.Object,
            cadastrosClient.Object,
            (eventPublisher ?? new Mock<IEventPublisher>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object,
            new Mock<IAppLogger<GerarOrcamentoUseCase>>().Object);
    }

    [Fact]
    public async Task Deve_falhar_quando_peca_nao_existe_no_cadastros()
    {
        // Arrange
        var orcamentoRepo = new Mock<IOrcamentoRepository>();
        var cadastrosClient = new Mock<ICadastrosServiceClient>();

        orcamentoRepo.Setup(r => r.ObterPorOsIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Domain.Entities.Orcamento?)null);
        cadastrosClient.Setup(c => c.ObterPrecoPecaAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((PrecoItem?)null);

        var useCase = CriarUseCase(orcamentoRepo, cadastrosClient);
        var request = new GerarOrcamentoRequest(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<ItemDiagnostico> { new(Guid.NewGuid(), 1) },
            new List<ItemDiagnostico>());

        // Act
        var resultado = await useCase.ExecutarAsync(request);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Contain("não encontrada");
    }

    [Fact]
    public async Task Deve_somar_pecas_e_servicos_e_publicar_orcamento_gerado()
    {
        // Arrange
        var orcamentoRepo = new Mock<IOrcamentoRepository>();
        var cadastrosClient = new Mock<ICadastrosServiceClient>();
        var eventPublisher = new Mock<IEventPublisher>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var pecaId = Guid.NewGuid();
        var servicoId = Guid.NewGuid();

        orcamentoRepo.Setup(r => r.ObterPorOsIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Domain.Entities.Orcamento?)null);
        cadastrosClient.Setup(c => c.ObterPrecoPecaAsync(pecaId, default))
            .ReturnsAsync(new PrecoItem(pecaId, "Pastilha de freio", 150m));
        cadastrosClient.Setup(c => c.ObterPrecoServicoAsync(servicoId, default))
            .ReturnsAsync(new PrecoItem(servicoId, "Troca de óleo", 200m));

        var useCase = CriarUseCase(orcamentoRepo, cadastrosClient, eventPublisher, unitOfWork);
        var request = new GerarOrcamentoRequest(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<ItemDiagnostico> { new(pecaId, 2) },      // 2 x 150 = 300
            new List<ItemDiagnostico> { new(servicoId, 1) });   // 1 x 200 = 200

        // Act
        var resultado = await useCase.ExecutarAsync(request);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor!.ValorTotal.Should().Be(500m);
        eventPublisher.Verify(p => p.EnfileirarAsync(It.IsAny<object>(), default), Times.AtLeastOnce);
        unitOfWork.Verify(u => u.SalvarAlteracoesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Deve_ser_idempotente_quando_orcamento_ja_existe_para_a_os()
    {
        // Arrange
        var orcamentoRepo = new Mock<IOrcamentoRepository>();
        var cadastrosClient = new Mock<ICadastrosServiceClient>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var osId = Guid.NewGuid();
        var itens = new[] { Domain.Entities.OrcamentoItem.Criar(Domain.Enums.TipoItem.Servico, Guid.NewGuid(), "Alinhamento", 1, 120m) };
        var existente = Domain.Entities.Orcamento.Gerar(osId, itens);

        orcamentoRepo.Setup(r => r.ObterPorOsIdAsync(osId, default)).ReturnsAsync(existente);

        var useCase = CriarUseCase(orcamentoRepo, cadastrosClient, unitOfWork: unitOfWork);
        var request = new GerarOrcamentoRequest(osId, Guid.NewGuid(), new List<ItemDiagnostico>(), new List<ItemDiagnostico>());

        // Act
        var resultado = await useCase.ExecutarAsync(request);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        orcamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Domain.Entities.Orcamento>(), default), Times.Never);
        unitOfWork.Verify(u => u.SalvarAlteracoesAsync(default), Times.Never);
    }
}
