namespace ReciveAPI.Services.IServices
{
    public interface IFileProcessingQueueServices
    {
        void Enqueue(string fileContent);
        bool TryDequeue(out string fileContent);
    }
}
