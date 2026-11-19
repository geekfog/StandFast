using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class OrganizationSettingsServiceTests
{
    private readonly InMemoryOrganizationSettingsRepository repository = new();
    private readonly OrganizationSettingsService service;

    public OrganizationSettingsServiceTests() => service = new OrganizationSettingsService(repository, new FixedClock(BoardTestContext.Now), new RecordingAuditLog());

    [Fact]
    public async Task NothingSaved_ReadsTheDefault_WhichKeepsTheStarredPersonInTheirSortedPlace()
    {
        Assert.Equal(OrganizationSettingsDto.Default, await service.GetAsync());
        Assert.False(OrganizationSettingsDto.Default.SelfFirstOnBoard);
    }

    [Fact]
    public async Task TurningOnSelfFirst_IsKept_AndCanBeTurnedOffAgain()
    {
        await service.SetSelfFirstOnBoardAsync(true);
        Assert.True((await service.GetAsync()).SelfFirstOnBoard);

        await service.SetSelfFirstOnBoardAsync(false);
        Assert.False((await service.GetAsync()).SelfFirstOnBoard);
    }

    private sealed class InMemoryOrganizationSettingsRepository : IOrganizationSettingsRepository
    {
        private OrganizationSettings? settings;

        public Task<OrganizationSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task UpsertAsync(OrganizationSettings organizationSettings, CancellationToken cancellationToken = default)
        {
            settings = organizationSettings;
            return Task.CompletedTask;
        }
    }
}
