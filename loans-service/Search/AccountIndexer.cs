using Elastic.Clients.Elasticsearch;
using loans_service.Events;

namespace loans_service.Search;

public class AccountDocument
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class AccountIndexer
{
    public const string IndexName = "accounts";

    private readonly ElasticsearchClient _client;
    private readonly ILogger<AccountIndexer> _logger;

    public AccountIndexer(ElasticsearchClient client, ILogger<AccountIndexer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task IndexAsync(AccountEventData account, CancellationToken cancellationToken)
    {
        var document = new AccountDocument
        {
            Id = account.Id,
            Name = account.Name,
            Email = account.Email,
            Balance = account.Balance,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
        };

        var response = await _client.IndexAsync(
            document,
            i => i.Index(IndexName).Id(document.Id),
            cancellationToken);

        if (!response.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to index account {account.Id}: {response.DebugInformation}");
        }

        _logger.LogInformation("Indexed account {AccountId} into {Index}", account.Id, IndexName);
    }

    public async Task DeleteAsync(string accountId, CancellationToken cancellationToken)
    {
        var response = await _client.DeleteAsync(IndexName, new Id(accountId), cancellationToken);

        if (!response.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to delete account {accountId} from index: {response.DebugInformation}");
        }

        if (response.Result == Result.NotFound)
        {
            _logger.LogDebug("Account {AccountId} was not present in {Index}", accountId, IndexName);
        }
        else
        {
            _logger.LogInformation("Deleted account {AccountId} from {Index}", accountId, IndexName);
        }
    }
}
