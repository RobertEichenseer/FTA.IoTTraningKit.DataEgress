using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Text;
 
Console.WriteLine($"Launched from {Environment.CurrentDirectory}");
            Console.WriteLine($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"AppContext.BaseDir {AppContext.BaseDirectory}");
            Console.WriteLine($"Runtime Call {Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}");
        

IHost consoleHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddTransient<Main>();
    })
    .ConfigureAppConfiguration(configure =>{
        configure.SetBasePath(AppContext.BaseDirectory);
        configure.AddJsonFile("appsettings.json");
    })
    .Build();

Main main = consoleHost.Services.GetRequiredService<Main>();
await main.ExecuteAsync(args);

class Main
{
    ILogger<Main> _logger;
    IConfiguration _configuration;

    long _debugCounter = 0; 

    static BlobContainerClient _blobContainerClient; 
    static EventProcessorClient _eventProcessorClient;

    static EventProcessorClientBatch _eventProcessorClientBatch; 

    string _blobStorageConnectionString = ""; 
    string _blobContainerName = "";
    string _ehubNamespaceConnectionString = "";
    string _eventHubName = ""; 
    string _consumerGroup = "";  

    public Main(ILogger<Main> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _blobStorageConnectionString = _configuration["BlobStorageConnectionString"];
        _blobContainerName = _configuration["BlobContainerName"];
        _ehubNamespaceConnectionString = _configuration["HubNamespaceConnectionString"];
        _eventHubName = _configuration["HubName"];
        _consumerGroup = _configuration["HubConsumerGroup"]; 
    }

    public async Task ExecuteAsync(string[] args)
    {
        await ProcessSingleMessage(); 
        //await ProcessMessageBatch(); 
    }

    private async Task ProcessMessageBatch()
    {
        
        _blobContainerClient = new BlobContainerClient(_blobStorageConnectionString, _blobContainerName);
        EventProcessorClientBatch batch = new EventProcessorClientBatch(_blobContainerClient, _consumerGroup, _ehubNamespaceConnectionString, _eventHubName);
        
        batch.ProcessEventAsync += ProcessEventHandler; 
        batch.ProcessErrorAsync += ProcessErrorHandler; 
        await batch.StartProcessingAsync();    

        Console.WriteLine($"Press <ctrl>+<c> to stop listening!");
        while (true)
        {
            if (!Console.KeyAvailable){
                ConsoleKeyInfo consoleKeyInfo = Console.ReadKey(); 
                if  (consoleKeyInfo.Key == ConsoleKey.Escape ) {
                    await batch.StopProcessingAsync(); 
                }
            }
        }
    }

    async Task ProcessSingleMessage()
    {
        _blobContainerClient = new BlobContainerClient(_blobStorageConnectionString, _blobContainerName);
        _eventProcessorClient = new EventProcessorClient(_blobContainerClient, _consumerGroup, _ehubNamespaceConnectionString, _eventHubName);
        
        _eventProcessorClient.PartitionInitializingAsync += ProcessInitHandler; 
        _eventProcessorClient.ProcessEventAsync += ProcessEventHandler;
        _eventProcessorClient.ProcessErrorAsync += ProcessErrorHandler;

        await _eventProcessorClient.StartProcessingAsync(); 

        Console.WriteLine($"Press <ctrl>+<c> to stop listening!");
        while (true)
        {
            if (!Console.KeyAvailable){
                ConsoleKeyInfo consoleKeyInfo = Console.ReadKey(); 
                if  (consoleKeyInfo.Key == ConsoleKey.Escape ) {
                    await _eventProcessorClient.StopProcessingAsync();
                }
            }
        }
    }

    private Task ProcessInitHandler(PartitionInitializingEventArgs arg)
    {
        if (arg.CancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        //Start position if no check point is available
        // EventPosition startPosition = EventPosition.FromEnqueuedTime(DateTimeOffset.Now); 
        // arg.DefaultStartingPosition = startPosition; 

        return Task.CompletedTask;
    
    }

    async Task ProcessEventHandler(ProcessEventArgs eventArgs)
    {
        _debugCounter ++; 
        Console.WriteLine($"Message received event: {_debugCounter} - {Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray())}");
        await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
    }

    Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
    {
        Console.WriteLine($"\tPartition '{eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
        Console.WriteLine(eventArgs.Exception.Message);

        return Task.CompletedTask;
    }

}

public class EventProcessorClientBatch : EventProcessorClient {

    public EventProcessorClientBatch (BlobContainerClient blobContainerClient, 
                                        string consumerGroup, 
                                        string ehubNamespaceConnectionString, 
                                        string eventHubName) 
        : base(blobContainerClient, consumerGroup, ehubNamespaceConnectionString, eventHubName )
    { }

    protected override async Task OnProcessingEventBatchAsync (IEnumerable<EventData> events, EventProcessorPartition partition, CancellationToken cancellationToken) 
    {

        Console.WriteLine($"\tBatch of messages received: {events.Count()}"); 
        foreach (EventData eventData in events) {
             
            Console.WriteLine($"{eventData.EventBody.ToString()}");
        };
        List<EventData> lastProcessedEvent = new List<EventData>(); 
        lastProcessedEvent.Add(events.Last<EventData>()); 
        //Checkpoint last message from batch
        await base.OnProcessingEventBatchAsync(lastProcessedEvent, partition, cancellationToken); 
        //Checkpoint every single message
        //await base.OnProcessingEventBatchAsync(events, partition, cancellationToken); 
    }
}
