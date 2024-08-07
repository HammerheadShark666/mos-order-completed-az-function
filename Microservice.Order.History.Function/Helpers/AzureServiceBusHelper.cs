﻿using Azure.Messaging.ServiceBus;
using Microservice.Order.History.Function.Helpers.Interfaces;
using Microsoft.Extensions.Logging;

namespace Microservice.Order.History.Function.Helpers;

public class AzureServiceBusHelper : IAzureServiceBusHelper
{
    private readonly ILogger<AzureServiceBusHelper> _logger;

    public AzureServiceBusHelper(ILogger<AzureServiceBusHelper> logger) => _logger = logger;

    public async Task SendMessage(string queue, string data)
    {  
        var client = new ServiceBusClient(EnvironmentVariables.AzureServiceBusConnection);
        var sender = client.CreateSender(queue);  

        await sender.SendMessageAsync(new ServiceBusMessage(data));
    } 
}