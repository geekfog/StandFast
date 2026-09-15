using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class PersonRepository(ITableClientProvider tables) : IPersonRepository
{
    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.People, cancellationToken);
        List<Person> people = [];

        await foreach (PersonTableEntity entity in client.QueryAsync<PersonTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.PersonPartition}"), cancellationToken: cancellationToken))
        {
            people.Add(entity.ToDomain());
        }

        return people;
    }

    public async Task<Person?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.People, cancellationToken);
        NullableResponse<PersonTableEntity> response = await client.GetEntityIfExistsAsync<PersonTableEntity>(StorageKeys.PersonPartition, StorageKeys.PersonRowKey(id), cancellationToken: cancellationToken);
        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertAsync(Person person, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.People, cancellationToken);
        await client.UpsertEntityAsync(person.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.People, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.PersonPartition, StorageKeys.PersonRowKey(id), ETag.All, cancellationToken);
    }
}
