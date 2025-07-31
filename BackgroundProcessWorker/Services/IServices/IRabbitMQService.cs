namespace BackgroundProcessWorker.Services.IServices
{
    public interface IRabbitMQService
    {
        public void StartListening(Action<string> messageHandler);

    }
}
