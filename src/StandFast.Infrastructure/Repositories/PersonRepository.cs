using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
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
        await DeletePhotoAsync(id, cancellationToken);

        foreach (PersonDayOff dayOff in await GetDaysOffAsync(id, cancellationToken))
        {
            await DeleteDayOffAsync(id, dayOff.Id, cancellationToken);
        }
    }

    public async Task<PersonPhoto?> GetPhotoAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.PersonPhotos, cancellationToken);
        NullableResponse<PersonPhotoTableEntity> response =
            await client.GetEntityIfExistsAsync<PersonPhotoTableEntity>(StorageKeys.PersonPartition, StorageKeys.PersonRowKey(personId), cancellationToken: cancellationToken);
        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertPhotoAsync(PersonPhoto photo, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.PersonPhotos, cancellationToken);
        await client.UpsertEntityAsync(photo.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    /// <summary>Deleting a row that does not exist succeeds, so this is safe whether or not a photo was ever saved.</summary>
    public async Task DeletePhotoAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.PersonPhotos, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.PersonPartition, StorageKeys.PersonRowKey(personId), ETag.All, cancellationToken);
    }

    public Task<IReadOnlyList<PersonDayOff>> GetDaysOffAsync(Guid personId, CancellationToken cancellationToken = default) =>
        QueryDaysOffAsync(TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.PersonChildPartitionKey(personId)}"), cancellationToken);

    /// <summary>Crosses every person's partition. Date keys sort as dates, so the overlap test runs in storage and returns only the spans that touch the range.</summary>
    public Task<IReadOnlyList<PersonDayOff>> GetDaysOffAsync(DateRange dates, CancellationToken cancellationToken = default) =>
        QueryDaysOffAsync(TableClient.CreateQueryFilter($"StartDate le {MeetingCalendar.ToDateKey(dates.End)} and EndDate ge {MeetingCalendar.ToDateKey(dates.Start)}"), cancellationToken);

    public async Task UpsertDayOffAsync(PersonDayOff dayOff, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.PersonDaysOff, cancellationToken);
        await client.UpsertEntityAsync(dayOff.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteDayOffAsync(Guid personId, Guid dayOffId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.PersonDaysOff, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.PersonChildPartitionKey(personId), StorageKeys.DayOffRowKey(dayOffId), ETag.All, cancellationToken);
    }

    private async Task<IReadOnlyList<PersonDayOff>> QueryDaysOffAsync(string filter, CancellationToken cancellationToken)
    {
        TableClient client = await tables.GetAsync(StorageNames.PersonDaysOff, cancellationToken);
        List<PersonDayOff> daysOff = [];

        await foreach (PersonDayOffTableEntity entity in client.QueryAsync<PersonDayOffTableEntity>(filter, cancellationToken: cancellationToken))
        {
            daysOff.Add(entity.ToDomain());
        }

        return daysOff;
    }
}
