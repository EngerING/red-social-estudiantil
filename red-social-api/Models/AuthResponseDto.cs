namespace RedSocialApi.Models
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
    }
}