using ReciveAPI.Services.IServices;
using System.Collections.Concurrent;

namespace ReciveAPI.Services
{
    public class FileProcessingQueueServices : IFileProcessingQueueServices
    {
        private readonly ConcurrentQueue<string> _queue = new();
        public void Enqueue(string fileContent)
        {
            _queue.Enqueue(fileContent);
        }

        public bool TryDequeue(out string fileContent)
        {
            return _queue.TryDequeue(out fileContent);
        }
    }
}
