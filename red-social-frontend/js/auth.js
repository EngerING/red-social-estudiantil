const API_URL = "http://localhost:5000/api/Auth";

document.addEventListener("DOMContentLoaded", () => {
  const loginForm = document.getElementById("loginForm");
  const registerForm = document.getElementById("registerForm");

  if (loginForm) {
    loginForm.addEventListener("submit", loginUser);
  }

  if (registerForm) {
    registerForm.addEventListener("submit", registerUser);
  }
});

async function loginUser(e) {
  e.preventDefault();

  const message = document.getElementById("loginMessage");
  message.textContent = "Validando acceso...";
  message.className = "message";

  const payload = {
    email: document.getElementById("loginEmail").value.trim(),
    password: document.getElementById("loginPassword").value.trim()
  };

  try {
    const response = await fetch(`${API_URL}/login`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload)
    });

    const data = await response.json();

    if (!response.ok) {
      message.textContent = data.message || "No se pudo iniciar sesión";
      message.className = "message error";
      return;
    }

    localStorage.setItem("token", data.token || "");
    localStorage.setItem("userId", data.userId || "");
    localStorage.setItem("email", data.email || "");

    message.textContent = "Login exitoso. Redirigiendo...";
    message.className = "message success";

    setTimeout(() => {
      window.location.href = "feed.html";
    }, 1000);
  } catch (error) {
    message.textContent = "Error de conexión con la API";
    message.className = "message error";
  }
}

async function registerUser(e) {
  e.preventDefault();

  const message = document.getElementById("registerMessage");
  message.textContent = "Creando cuenta...";
  message.className = "message";

  const payload = {
    nombre: document.getElementById("nombre").value.trim(),
    apellido: document.getElementById("apellido").value.trim(),
    email: document.getElementById("email").value.trim(),
    carrera: document.getElementById("carrera").value.trim(),
    password: document.getElementById("password").value.trim()
  };

  try {
    const response = await fetch(`${API_URL}/register`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload)
    });

    const data = await response.json();

    if (!response.ok) {
      message.textContent = data.message || "No se pudo crear la cuenta";
      message.className = "message error";
      return;
    }

    message.textContent = "Cuenta creada correctamente. Redirigiendo al login...";
    message.className = "message success";

    setTimeout(() => {
      window.location.href = "login.html";
    }, 1400);
  } catch (error) {
    message.textContent = "Error de conexión con la API";
    message.className = "message error";
  }
}