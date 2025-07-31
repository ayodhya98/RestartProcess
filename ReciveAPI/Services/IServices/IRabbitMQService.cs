namespace ReciveAPI.Services.IServices
{
    public interface IRabbitMQService
    {
        public void SendMessage(string message);
    }
}
