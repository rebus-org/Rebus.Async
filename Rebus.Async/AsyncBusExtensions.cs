﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Internals;
using Rebus.Messages;
using Rebus.Transport;
// ReSharper disable ArgumentsStyleLiteral

#pragma warning disable 4014

namespace Rebus;

/// <summary>
/// Configuration and bus extensions for enabling async/await-based request/reply
/// </summary>
public static class AsyncBusExtensions
{
    internal static readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> Messages = new();

    /// <summary>
    /// Extension method on <see cref="IBus"/> that allows for asynchronously sending a request and dispatching
    /// the received reply to the continuation.
    /// </summary>
    /// <typeparam name="TReply">Specifies the expected type of the reply. Can be any type compatible with the actually received reply</typeparam>
    /// <param name="bus">The bus instance to use to send the request</param>
    /// <param name="request">The request message</param>
    /// <param name="optionalHeaders">Headers to be included in the request message</param>
    /// <param name="timeout">Optionally specifies the max time to wait for a reply. If this time is exceeded, a <see cref="TimeoutException"/> is thrown</param>
    /// <param name="externalCancellationToken">An external cancellation token from some outer context that cancels waiting for a reply</param>
    /// <returns></returns>
    public static async Task<TReply> SendRequest<TReply>(this IBus bus, object request, IDictionary<string, string> optionalHeaders = null, TimeSpan? timeout = null, CancellationToken externalCancellationToken = default)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var currentTransactionContext = AmbientTransactionContext.Current;
        try
        {
            AmbientTransactionContext.SetCurrent(null);

            Task BusAction(object obj, IDictionary<string, string> headers) => bus.Send(obj, headers);

            return await InnerProcessRequest<TReply>(BusAction, request, optionalHeaders, timeout, externalCancellationToken);
        }
        finally
        {
            AmbientTransactionContext.SetCurrent(currentTransactionContext);
        }
    }

    /// <summary>
    /// Extension method on <see cref="IBus"/> that allows for asynchronously sending a request and dispatching
    /// the received reply to the continuation.
    /// </summary>
    /// <typeparam name="TReply">Specifies the expected type of the reply. Can be any type compatible with the actually received reply</typeparam>
    /// <param name="routingApi">The routing API to use when sending the request</param>
    /// <param name="destinationAddress">The destination queue to send the request to</param>
    /// <param name="request">The request message</param>
    /// <param name="optionalHeaders">Headers to be included in the request message</param>
    /// <param name="timeout">Optionally specifies the max time to wait for a reply. If this time is exceeded, a <see cref="TimeoutException"/> is thrown</param>
    /// <param name="externalCancellationToken">An external cancellation token from some outer context that cancels waiting for a reply</param>
    /// <returns></returns>
    public static async Task<TReply> SendRequest<TReply>(this IRoutingApi routingApi, string destinationAddress, object request, IDictionary<string, string> optionalHeaders = null, TimeSpan? timeout = null, CancellationToken externalCancellationToken = default)
    {
        if (routingApi == null) throw new ArgumentNullException(nameof(routingApi));
        if (request == null) throw new ArgumentNullException(nameof(request));
        var currentTransactionContext = AmbientTransactionContext.Current;
        try
        {
            AmbientTransactionContext.SetCurrent(null);

            Task BusAction(object obj, IDictionary<string, string> headers) => routingApi.Send(destinationAddress, obj, headers);

            return await InnerProcessRequest<TReply>(BusAction, request, optionalHeaders, timeout, externalCancellationToken);
        }
        finally
        {
            AmbientTransactionContext.SetCurrent(currentTransactionContext);
        }
    }

    static async Task<TReply> InnerProcessRequest<TReply>(Func<object, IDictionary<string, string>, Task> busAction, object request, IDictionary<string, string> optionalHeaders, TimeSpan? timeout, CancellationToken externalCancellationToken)
    {
        var maxWaitTime = timeout ?? TimeSpan.FromSeconds(5);

        if (TryGrabDesiredMessageId(optionalHeaders, out var messageId))
        {
            if (!messageId.StartsWith(ReplyHandlerStep.SpecialMessageIdPrefix))
            {
                messageId = $"{ReplyHandlerStep.SpecialMessageIdPrefix}_{messageId}";
            }
        }
        else
        {
            messageId = $"{ReplyHandlerStep.SpecialMessageIdPrefix}_{Guid.NewGuid()}";
        }

        var headers = new Dictionary<string, string>
        {
            {Headers.MessageId, messageId}
        };

        if (optionalHeaders != null)
        {
            foreach (var kvp in optionalHeaders)
            {
                try
                {
                    headers.Add(kvp.Key, kvp.Value);
                }
                catch (Exception exception)
                {
                    var headersString = string.Join(", ", headers.Select(k => $"{k.Key}={k.Value}"));

                    throw new ArgumentException(
                        $"Could not add key-value-pair {kvp.Key}={kvp.Value} to headers because the key was already taken: {headersString}",
                        exception);
                }
            }
        }

        var taskCompletionSource = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = taskCompletionSource.Task;

        using (var timeoutCancellationTokenSource = new CancellationTokenSource(maxWaitTime))
        using (var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, externalCancellationToken))
        {
            var timeoutCancellationToken = timeoutCancellationTokenSource.Token;
            var cancellationToken = cancellationTokenSource.Token;

            cancellationToken.Register(() =>
            {
                // if we could successfully remove the completion source again, it means that the reply was NOT received...
                // therefore, it's safe to cancel the task
                if (Messages.TryRemove(messageId, out _))
                {
                    taskCompletionSource.SetCanceled();
                }
            }, useSynchronizationContext: false);

            Messages.TryAdd(messageId, taskCompletionSource);

            await busAction(request, headers);

            try
            {
                var result = await task;

                if (result.Body is TReply reply)
                {
                    return reply;
                }

                throw new InvalidCastException($"Could not return message {messageId} as a {typeof(TReply)}");
            }
            catch (OperationCanceledException) when (timeoutCancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Did not receive reply for request with in-reply-to ID '{messageId}' within {maxWaitTime} timeout");
            }
        }
    }

    static bool TryGrabDesiredMessageId(IDictionary<string, string> optionalHeaders, out string messageId)
    {
        messageId = null;

        if (optionalHeaders != null && optionalHeaders.TryGetValue(Headers.MessageId, out messageId))
        {
            optionalHeaders.Remove(Headers.MessageId);
        }

        return messageId != null;
    }
}