namespace OMS.API.Infrastructure.Seeders;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
