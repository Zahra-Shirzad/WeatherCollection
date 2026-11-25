
using Microsoft.EntityFrameworkCore;
using System.Data;
using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Domain.Entities;
using WeatherCollection.Domain.Enums;

namespace WeatherCollection.Infrastructure.Persistence.Repositories;

public sealed class WeatherWorkRepository : IWeatherWorkRepository
{
    private readonly WeatherCollectionDbContext _context;

    public WeatherWorkRepository(WeatherCollectionDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WeatherLocation>> ClaimNextBatchAsync(
        string workerId,
        int batchSize,
        int maxAttempts,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID is required.", nameof(workerId));

        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var now = DateTime.UtcNow;
        var leaseUntil = now.Add(leaseDuration);

        var connection = _context.Database.GetDbConnection();

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SET NOCOUNT ON;

            DECLARE @AffectedRequests TABLE
            (
                RequestId UNIQUEIDENTIFIER
            );

            DECLARE @ClaimedLocations TABLE
            (
                Id UNIQUEIDENTIFIER,
                RequestId UNIQUEIDENTIFIER
            );

            -- 1. Mark expired leases as failed when attempts are exhausted.
            UPDATE dbo.WeatherLocations
            SET
                Status = 'Failed',
                LeaseUntilUtc = NULL,
                ClaimedBy = NULL,
                NextAttemptAtUtc = NULL,
                LastError = COALESCE(
                    NULLIF(LastError, ''),
                    'Worker lease expired after maximum attempts.'
                )
            OUTPUT inserted.WeatherRequestId
            INTO @AffectedRequests(RequestId)
            WHERE Status = 'Processing'
              AND LeaseUntilUtc <= @Now
              AND AttemptCount >= @MaxAttempts;

            -- 2. Claim available locations.
            ;WITH Candidates AS
            (
                SELECT TOP (@BatchSize) *
                FROM dbo.WeatherLocations WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE AttemptCount < @MaxAttempts
                  AND
                  (
                      (
                          Status = 'Pending'
                          AND (
                              NextAttemptAtUtc IS NULL
                              OR NextAttemptAtUtc <= @Now
                          )
                      )
                      OR
                      (
                          Status = 'Processing'
                          AND LeaseUntilUtc <= @Now
                      )
                  )
                ORDER BY CreatedAtUtc, Sequence
            )
            UPDATE Candidates
            SET
                Status = 'Processing',
                AttemptCount = AttemptCount + 1,
                ClaimVersion = ClaimVersion + 1,
                ClaimedBy = @WorkerId,
                LeaseUntilUtc = @LeaseUntil,
                NextAttemptAtUtc = NULL
            OUTPUT inserted.Id, inserted.WeatherRequestId
            INTO @ClaimedLocations(Id, RequestId);

            -- 3. Track affected requests.
            INSERT INTO @AffectedRequests(RequestId)
            SELECT DISTINCT RequestId
            FROM @ClaimedLocations;

            -- 4. Recalculate affected request statuses.
            UPDATE R
            SET
                StartedAtUtc =
                    CASE
                        WHEN A.Processing > 0 OR A.Completed > 0 OR A.Attempted > 0
                        THEN COALESCE(R.StartedAtUtc, @Now)
                        ELSE R.StartedAtUtc
                    END,

                Status =
                    CASE
                        WHEN A.Total = 0 THEN R.Status
                        WHEN A.Completed = A.Total AND A.Succeeded = A.Total
                            THEN 'Completed'
                        WHEN A.Completed = A.Total AND A.Failed = A.Total
                            THEN 'Failed'
                        WHEN A.Completed = A.Total
                            THEN 'PartiallyCompleted'
                        WHEN A.Processing > 0 OR A.Attempted > 0 OR A.Completed > 0
                            THEN 'Processing'
                        ELSE 'Pending'
                    END,

                CompletedAtUtc =
                    CASE
                        WHEN A.Total > 0 AND A.Completed = A.Total
                            THEN COALESCE(R.CompletedAtUtc, @Now)
                        ELSE NULL
                    END
            FROM dbo.WeatherRequests R
            CROSS APPLY
            (
                SELECT
                    COUNT_BIG(*) AS Total,
                    COALESCE(SUM(CASE WHEN L.Status = 'Succeeded' THEN 1 ELSE 0 END), 0) AS Succeeded,
                    COALESCE(SUM(CASE WHEN L.Status = 'Failed' THEN 1 ELSE 0 END), 0) AS Failed,
                    COALESCE(SUM(CASE WHEN L.Status = 'Processing' THEN 1 ELSE 0 END), 0) AS Processing,
                    COALESCE(SUM(CASE WHEN L.AttemptCount > 0 THEN 1 ELSE 0 END), 0) AS Attempted,
                    COALESCE(SUM(CASE WHEN L.Status IN ('Succeeded', 'Failed') THEN 1 ELSE 0 END), 0) AS Completed
                FROM dbo.WeatherLocations L
                WHERE L.WeatherRequestId = R.Id
            ) A
            WHERE EXISTS
            (
                SELECT 1
                FROM @AffectedRequests AR
                WHERE AR.RequestId = R.Id
            );

            -- 5. Return claimed location IDs.
            SELECT Id
            FROM @ClaimedLocations;
            """;

        AddParameter(command, "@BatchSize", batchSize);
        AddParameter(command, "@MaxAttempts", maxAttempts);
        AddParameter(command, "@Now", now);
        AddParameter(command, "@WorkerId", workerId);
        AddParameter(command, "@LeaseUntil", leaseUntil);

        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var claimedIds = new List<Guid>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
                claimedIds.Add(reader.GetGuid(0));

            if (claimedIds.Count == 0)
                return Array.Empty<WeatherLocation>();

            return await _context.WeatherLocations
                .Where(x => claimedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid locationId,
        long claimVersion,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var now = DateTime.UtcNow;
        var newLeaseUntil = now.Add(leaseDuration);

        var affectedRows = await _context.WeatherLocations
            .Where(x =>
                x.Id == locationId &&
                x.Status == WeatherLocationStatus.Processing &&
                x.ClaimVersion == claimVersion &&
                x.LeaseUntilUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.LeaseUntilUtc,
                    newLeaseUntil),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkSucceededAsync(
        Guid locationId,
        long claimVersion,
        WeatherResult result,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var location = await _context.WeatherLocations
            .Include(x => x.Result)
            .FirstOrDefaultAsync(
                x => x.Id == locationId &&
                     x.Status == WeatherLocationStatus.Processing &&
                     x.ClaimVersion == claimVersion,
                cancellationToken);

        if (location is null)
            return false;

        if (result.WeatherLocationId != locationId)
            throw new ArgumentException(
                "Result belongs to a different location.", nameof(result));

        location.MarkSucceeded(claimVersion);

        var request = await _context.WeatherRequests
            .Include(x => x.Locations)
            .FirstAsync(
                x => x.Id == location.WeatherRequestId,
                cancellationToken);

        request.RecalculateStatus();

        await _context.WeatherResults.AddAsync(result, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid locationId,
        long claimVersion,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var location = await _context.WeatherLocations
            .FirstOrDefaultAsync(
                x => x.Id == locationId &&
                     x.Status == WeatherLocationStatus.Processing &&
                     x.ClaimVersion == claimVersion,
                cancellationToken);

        if (location is null)
            return false;

        location.ScheduleRetry(
            claimVersion,
            nextAttemptAtUtc,
            error);

        var request = await _context.WeatherRequests
            .Include(x => x.Locations)
            .FirstAsync(
                x => x.Id == location.WeatherRequestId,
                cancellationToken);

        request.RecalculateStatus();

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ReleaseWithoutConsumingAttemptAsync(
        Guid locationId,
        long claimVersion,
        DateTime nextAttemptAtUtc,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var location = await _context.WeatherLocations
            .FirstOrDefaultAsync(
                x => x.Id == locationId &&
                     x.Status == WeatherLocationStatus.Processing &&
                     x.ClaimVersion == claimVersion,
                cancellationToken);

        if (location is null)
            return false;

        location.ReleaseWithoutConsumingAttempt(
            claimVersion,
            nextAttemptAtUtc,
            error);

        var request = await _context.WeatherRequests
            .Include(x => x.Locations)
            .FirstAsync(
                x => x.Id == location.WeatherRequestId,
                cancellationToken);

        request.RecalculateStatus();

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<bool> MarkFailedAsync(
        Guid locationId,
        long claimVersion,
        string error,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var location = await _context.WeatherLocations
            .FirstOrDefaultAsync(
                x => x.Id == locationId &&
                     x.Status == WeatherLocationStatus.Processing &&
                     x.ClaimVersion == claimVersion,
                cancellationToken);

        if (location is null)
            return false;

        location.MarkFailed(claimVersion, error);

        var request = await _context.WeatherRequests
            .Include(x => x.Locations)
            .FirstAsync(
                x => x.Id == location.WeatherRequestId,
                cancellationToken);

        request.RecalculateStatus();

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}
