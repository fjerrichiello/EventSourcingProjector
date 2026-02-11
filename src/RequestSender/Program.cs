// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Net.Http.Json;
using RequestSender;

var _httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};

var targetUrlFormat = "http://localhost:5001/api/accounts/{0}/transactions";

int totalRequests = 1000;
int maxConcurrency = 10;

var semaphore = new SemaphoreSlim(maxConcurrency);
var tasks = new List<Task>();

int success = 0;
int failure = 0;

var stopwatch = Stopwatch.StartNew();

List<int> amounts = [100, 500, 1000, 5000];

for (int i = 0; i < totalRequests; i++)
{
    await semaphore.WaitAsync();

    tasks.Add(Task.Run(async () =>
    {
        try
        {
            var accountId = Random.Shared.Next(1, 1000);

            var transactionType = Random.Shared.Next(1, 100) % 2 == 1 ? "credit" : "debit";

            var amount = amounts[Random.Shared.Next(0, 3)];

            var request = new TransactionRequest(transactionType, amount, null);

            var targetUrl = string.Format(targetUrlFormat, accountId);

            var response = await _httpClient.PostAsJsonAsync(targetUrl, request);
            Console.WriteLine(response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            if (response.IsSuccessStatusCode)
                Interlocked.Increment(ref success);
            else
                Interlocked.Increment(ref failure);
        }
        catch
        {
            Interlocked.Increment(ref failure);
        }
        finally
        {
            semaphore.Release();
        }
    }));
}

await Task.WhenAll(tasks);

stopwatch.Stop();

Console.WriteLine("=== Load Test Complete ===");
Console.WriteLine($"Total Requests: {totalRequests}");
Console.WriteLine($"Success: {success}");
Console.WriteLine($"Failure: {failure}");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");
Console.WriteLine($"Req/sec: {totalRequests / stopwatch.Elapsed.TotalSeconds:F2}");