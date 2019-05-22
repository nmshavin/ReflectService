using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ReflectService.Hubs
{
    public class ReflectServiceHub : Hub
    {
        public async Task SendMessage(object callbackData)
        {
            await Clients.All.SendAsync("ReceiveCallback", callbackData);
        }
    }
}