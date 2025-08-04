using ReciveAPI.Services.IServices;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ReciveAPI.Services
{
    public class FileProcessingQueueServices : IFileProcessingQueueServices
    {
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly ActivitySource _activitySource;
        private int _queueSize = 0;

        public FileProcessingQueueServices()
        {
            _activitySource = new ActivitySource("FileProcessingQueueServices");
        }

        public void Enqueue(string filePath)
        {
            var activity = _activitySource.StartActivity("Queue.Enqueue");
            activity?.AddTag("file.path", filePath);
            activity?.AddTag("queue.operation", "enqueue");

            _queue.Enqueue(filePath);

            var newSize = Interlocked.Increment(ref _queueSize);

            activity?.AddTag("queue.size.after", newSize);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        public bool TryDequeue(out string filePath)
        {

            var activity = _activitySource.StartActivity("Queue.TryDequeue");
            activity?.AddTag("queue.operation", "try_dequeue");

            var success = _queue.TryDequeue(out filePath);

            if (success)
            {
                var newSize = Interlocked.Decrement(ref _queueSize);
                activity?.AddTag("file.path", filePath);
                activity?.AddTag("queue.size.after", newSize);
                activity?.AddTag("dequeue.result", "success");
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity?.AddTag("dequeue.result", "empty_queue");
                activity?.SetStatus(ActivityStatusCode.Ok);
            }

            return success;
        }
    }
}
