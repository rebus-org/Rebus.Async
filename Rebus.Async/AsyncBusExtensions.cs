using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Rebus.Async;
using Rebus.Bus;
using Rebus.Messages;

namespace Rebus
{
    /// <summary>
    /// Configuration and bus extepsions for enabling async/await-based request/reply
    /// </summary>
    public static class AsyncBusExtensions
    {
        internal static readonly ConcurrentDictionary<string, TimedMessage> Messages = new ConcurrentDictionary<string, TimedMessage>();

        /// <summary>
        /// Extension method on <see cref="IBus"/> that allows for asynchronously sending a request and dispatching
        /// the received reply to the continuation.
        /// </summary>
        /// <typeparam name="TReply">Specifies the expected type of the reply. Can be any type compatible with the actually received reply</typeparam>
        /// <param name="bus">The bus instance to use to send the request</param>
        /// <param name="request">The request message</param>
        /// <param name="optionalHeaders">Headers to be included in the request message</param>
        /// <param name="timeout">Optionally specifies the max time to wait for a reply. If this time is exceeded, a <see cref="TimeoutException"/> is thrown</param>
        /// <returns></returns>
        public static async Task<TReply> SendRequest<TReply>(this IBus bus, object request, Dictionary<string, string> optionalHeaders = null, TimeSpan? timeout = null)
        {
            var maxWaitTime = timeout ?? TimeSpan.FromSeconds(5);
            var correlationId = $"{ReplyHandlerStep.SpecialCorrelationIdPrefix}:{Guid.NewGuid()}";

            var headers = new Dictionary<string, string>
            {
                {Headers.CorrelationId, correlationId},
                {ReplyHandlerStep.SpecialRequestTag, "request"}
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
                        throw new ArgumentException($"Could not add key-value-pair {kvp.Key}={kvp.Value} to headers", exception);
                    }
                }
            }

            var stopwatch = Stopwatch.StartNew();

            await bus.Send(request, headers);

            TimedMessage reply;

            while (!Messages.TryRemove(correlationId, out reply))
            {
                var elapsed = stopwatch.Elapsed;

                await Task.Delay(10);

                if (elapsed > maxWaitTime)
                {
                    throw new TimeoutException($"Did not receive reply for request with correlation ID '{correlationId}' within {maxWaitTime} timeout");
                }
            }

            var message = reply.Message;

            try
            {
                return (TReply)message.Body;
            }
            catch (InvalidCastException exception)
            {
                throw new InvalidCastException($"Could not return message {message.GetMessageLabel()} as a {typeof(TReply)}", exception);
            }
        }
    }
}
