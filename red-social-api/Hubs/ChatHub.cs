using Microsoft.AspNetCore.SignalR;
using RedSocialApi.Models;
using RedSocialApi.Services;

namespace RedSocialApi.Hubs
{
    public class ChatHub : Hub
    {
        private readonly FirebaseService _firebaseService;

        public ChatHub(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        public async Task SendMessage(CrearChatMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Contenido))
            {
                return;
            }

            var creado = await _firebaseService.CrearChatMessageAsync(dto);
            if (creado is null)
            {
                return;
            }

            await Clients.All.SendAsync("ReceiveMessage", creado);
        }
    }
}
