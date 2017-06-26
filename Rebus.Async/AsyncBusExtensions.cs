using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Async;
using Rebus.Bus;
using Rebus.Messages;
#pragma warning disable 4014

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
            var messageId = $"{ReplyHandlerStep.SpecialMessageIdPrefix}:{Guid.NewGuid()}";

            var headers = new Dictionary<string, string>
            {
                {Headers.MessageId, messageId},
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

            var timedOut = false;

            Task.Run(async () =>
            {
                await Task.Delay(maxWaitTime);
                Volatile.Write(ref timedOut, true);
            });

            await bus.Send(request, headers);

            TimedMessage reply;

            while (!Messages.TryRemove(messageId, out reply))
            {
                await Task.Delay(10);
                
                if (Volatile.Read(ref timedOut))
                {
                    throw new TimeoutException($"Did not receive reply for request with in-reply-to ID '{messageId}' within {maxWaitTime} timeout");
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
