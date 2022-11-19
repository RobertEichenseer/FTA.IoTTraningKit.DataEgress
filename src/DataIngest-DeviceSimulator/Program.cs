using Microsoft.Azure.Devices.Client;
using System.Text; 
using System.Text.Json; 

string deviceConnectionString = "HostName=IoTHubDataEgress821.azure-devices.net;DeviceId=device01;SharedAccessKey=YLxwpTQBuvr7oIU5O7WaEEZPp6jSyfiXBAHN3lwNkBc=";
using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

string telemetryData = JsonSerializer.Serialize(
    new {
        DeviceId = "device01",
        TelemetryId = Guid.NewGuid(),
        Pressure = 15.7,
        EnergyConsumption = 20.7,
        TelemetryTimeStamp = DateTime.UtcNow
    }
);

for (int i=0; i<21; i++) {
    var message = new Message(Encoding.ASCII.GetBytes(telemetryData)) {
        ContentType = "application/json",
        ContentEncoding = "utf-8"
    };
    message.Properties.Add("Error", "No");
    await deviceClient.SendEventAsync(message, new CancellationToken());
};








