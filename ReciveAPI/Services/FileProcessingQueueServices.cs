using ReciveAPI.Services.IServices;
using System.Collections.Concurrent;

namespace ReciveAPI.Services
{
    public class FileProcessingQueueServices : IFileProcessingQueueServices
    {
        private readonly ConcurrentQueue<string> _queue = new();
        public void Enqueue(string filePath)
        {
            _queue.Enqueue(filePath);
        }

        public bool TryDequeue(out string filePath)
        {
            return _queue.TryDequeue(out filePath);
        }
    }
}
