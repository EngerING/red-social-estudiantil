using Microsoft.AspNetCore.Mvc;

namespace server.Controllers
{
    public class AuthController : Controller
    {
        // Esto buscará automáticamente el archivo Views/Auth/Login.cshtml
        public IActionResult Login()
        {
            return View();
        }
    }
}