﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Threading;
#pragma warning disable 1998

namespace Rebus.Async
{
    [StepDocumentation("Handles replies to requests sent with bus.SendRequest")]
    class ReplyHandlerStep : IIncomingStep, IInitializable, IDisposable
    {
        readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _messages;
        readonly IAsyncTask _cleanupTask;
        readonly ILog _log;

        public ReplyHandlerStep(ConcurrentDictionary<string, TaskCompletionSource<Message>> messages, IRebusLoggerFactory rebusLoggerFactory, IAsyncTaskFactory asyncTaskFactory)
        {
            _messages = messages ?? throw new ArgumentNullException(nameof(messages));
            _log = rebusLoggerFactory?.GetLogger<ReplyHandlerStep>() ?? throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _cleanupTask = asyncTaskFactory?.Create("CleanupAbandonedRepliesTask", CleanupAbandonedReplies) ?? throw new ArgumentNullException(nameof(asyncTaskFactory));
        }

        public const string SpecialMessageIdPrefix = "request-reply";

        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            var message = context.Load<Message>();

            var hasInReplyToHeader = message.Headers.TryGetValue(Headers.InReplyTo, out var inReplyToMessageId);
            if (hasInReplyToHeader)
            {
                var isRequestReplyCorrelationId = inReplyToMessageId.StartsWith(SpecialMessageIdPrefix);
                if (isRequestReplyCorrelationId)
                {
                    // it's the reply!
                    if (_messages.TryGetValue(inReplyToMessageId, out var tcs))
                        tcs.SetResult(message);

                    return;
                }
            }

            await next();
        }

        public void Initialize()
        {
            _cleanupTask.Start();
        }

        public void Dispose()
        {
            _cleanupTask.Dispose();
        }

        async Task CleanupAbandonedReplies()
        {
            var messageList = _messages.ToList();

            var timedMessagesToRemove = messageList
                .Where(m => m.Value.Task.IsCompleted)
                .ToList();

            if (!timedMessagesToRemove.Any()) return;

            _log.Info(
                "Found {0} tasks which have completed removing them now!",
                timedMessagesToRemove.Count);

            foreach (var messageToRemove in timedMessagesToRemove)
            {
                _messages.TryRemove(messageToRemove.Key, out _);
            }
        }
    }
}