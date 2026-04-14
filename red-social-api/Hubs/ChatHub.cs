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

        public async Task JoinStudyGroup(string groupId, string userId)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (!await _firebaseService.UsuarioPerteneceAStudyGroupAsync(userId, groupId))
            {
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GetStudyGroupRoom(groupId));
        }

        public async Task LeaveStudyGroup(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetStudyGroupRoom(groupId));
        }

        public async Task SendStudyGroupMessage(CrearStudyGroupMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.GroupId) || string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Contenido))
            {
                return;
            }

            var created = await _firebaseService.CrearStudyGroupMessageAsync(dto);
            if (created is null)
            {
                return;
            }

            await Clients.Group(GetStudyGroupRoom(dto.GroupId)).SendAsync("ReceiveStudyGroupMessage", created);
        }

        private static string GetStudyGroupRoom(string groupId) => $"study-group:{groupId}";
    }
}
