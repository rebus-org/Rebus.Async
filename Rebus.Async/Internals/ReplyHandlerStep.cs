using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;

#pragma warning disable 1998

namespace Rebus.Internals;

[StepDocumentation("Handles replies to requests sent with bus.SendRequest")]
class ReplyHandlerStep : IIncomingStep
{
    readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _messages;
    readonly ILog _log;

    public ReplyHandlerStep(ConcurrentDictionary<string, TaskCompletionSource<Message>> messages, IRebusLoggerFactory rebusLoggerFactory)
    {
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _log = rebusLoggerFactory?.GetLogger<ReplyHandlerStep>() ?? throw new ArgumentNullException(nameof(rebusLoggerFactory));
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
                // if we could successfully remove the task completion source for the request message ID,
                // we can complete it here
                if (_messages.TryRemove(inReplyToMessageId, out var taskCompletionSource))
                {
                    taskCompletionSource.SetResult(message);

                    // abort anything else in the pipeline
                    return;
                }

                _log.Warn("Received message with message ID {messageId}, which was determined to be a reply to be handled as an inline request-reply reply (because of the {prefix} prefix), BUT the dictionary of task completion sources did NOT contain an entry for that ID",
                    inReplyToMessageId, SpecialMessageIdPrefix);
            }
        }

        await next();
    }
}