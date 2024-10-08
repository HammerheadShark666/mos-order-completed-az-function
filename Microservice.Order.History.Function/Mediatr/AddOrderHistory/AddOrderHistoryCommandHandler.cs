﻿using AutoMapper;
using MediatR;
using Microservice.Order.History.Function.Data.Repository.Interfaces;
using Microservice.Order.History.Function.Domain;
using Microservice.Order.History.Function.Helpers;
using Microservice.Order.History.Function.Helpers.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Transactions;

namespace Microservice.Order.History.Function.MediatR.AddOrderHistory;

public class AddOrderHistoryCommandHandler(IOrderHistoryRepository orderHistoryRepository,
                                    IAzureServiceBusHelper azureServiceBusHelper,
                                    IMapper mapper,
                                    ILogger<AddOrderHistoryCommandHandler> logger) : IRequestHandler<AddOrderHistoryRequest, AddOrderHistoryResponse>
{
    private record Order(Guid OrderId);

    public async Task<AddOrderHistoryResponse> Handle(AddOrderHistoryRequest addOrderHistoryRequest, CancellationToken cancellationToken)
    {
        var orderHistory = mapper.Map<OrderHistory>(addOrderHistoryRequest);

        if (!await orderHistoryRepository.ExistsAsync(orderHistory.Id))
        {
            using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            UpdateOrderHistoryItems(orderHistory);
            await SaveOrderHistoryAsync(orderHistory);

            using (var tx = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                await SendOrderHistoryAddedToServiceBusQueueAsync(orderHistory.Id);
                tx.Complete();
            }
            ts.Complete();
        }
        else
        {
            logger.LogWarning("OrderHistory record  already exists: {orderHistory.Id}.", orderHistory.Id);
        }

        return new AddOrderHistoryResponse();
    }

    private async Task SaveOrderHistoryAsync(Domain.OrderHistory orderHistory)
    {
        await orderHistoryRepository.AddAsync(orderHistory);
    }

    private static void UpdateOrderHistoryItems(Domain.OrderHistory orderHistory)
    {
        foreach (var orderItem in orderHistory.OrderItems)
        {
            orderItem.OrderId = orderHistory.Id;
        }
    }

    private async Task SendOrderHistoryAddedToServiceBusQueueAsync(Guid id)
    {
        await azureServiceBusHelper.SendMessage(EnvironmentVariables.AzureServiceBusQueueOrderHistoryAdded, GetSerializedOrder(id));
    }

    private static string GetSerializedOrder(Guid orderId)
    {
        return JsonSerializer.Serialize(new Order(orderId));
    }
}