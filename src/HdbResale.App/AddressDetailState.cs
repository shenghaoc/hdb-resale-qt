using HdbResale.Domain;

namespace HdbResale.App;

// UI-thread state shared by selection and explicit retry. A generation also rejects an old A response
// after selecting A -> B -> A, even though its key matches again.
internal sealed class AddressDetailState
{
    private string key = "";
    private int generation;
    internal AddressDetail? Detail { get; private set; }
    internal BuyerTrendData Trend { get; private set; } = BuyerTrendData.Empty;
    internal string Error { get; private set; } = "";
    internal bool Loading { get; private set; }
    internal bool CanRetry => key.Length > 0 && !Loading && Error.Length > 0;

    internal void Select(string selectedKey, YearMonth latest,
        Func<string, CancellationToken, Task<AddressDetail?>> fetch, ApiRequests requests, Action changed)
    {
        if (selectedKey == key) return;
        key = selectedKey;
        generation++;
        Detail = null;
        Trend = BuyerTrendData.Empty;
        Error = "";
        Loading = false;
        if (key.Length > 0) Start(latest, fetch, requests, changed);
    }

    internal void Retry(YearMonth latest, Func<string, CancellationToken, Task<AddressDetail?>> fetch,
        ApiRequests requests, Action changed)
    {
        if (CanRetry) Start(latest, fetch, requests, changed);
    }

    private void Start(YearMonth latest, Func<string, CancellationToken, Task<AddressDetail?>> fetch,
        ApiRequests requests, Action changed)
    {
        var requestKey = key;
        var requestGeneration = ++generation;
        Loading = true;
        Error = "";
        requests.Start(cancellation => fetch(requestKey, cancellation), loaded =>
        {
            if (requestGeneration != generation) return;
            Loading = false;
            Detail = loaded;
            Error = loaded is null ? "The API has no details for this address." : "";
            Trend = BuyerTrend.Build(loaded, latest);
            changed();
        }, error =>
        {
            if (requestGeneration != generation) return;
            Loading = false;
            Error = "Could not load this address's registrations. " +
                (error is WorkerApiException ? error.Message : "Unexpected error: " + error.GetType().Name);
            changed();
        });
        changed();
    }
}
