const AUTH_API_URL = "http://localhost:5000/api/Auth";
const POSTS_API_URL = "http://localhost:5000/api/Posts";

const token = localStorage.getItem("token");
const userId = localStorage.getItem("userId");
const emailGuardado = localStorage.getItem("email");

if (!token || !userId) {
  window.location.href = "login.html";
}

async function cargarPerfil() {
  try {
    const response = await fetch(`${AUTH_API_URL}/profile/${userId}`, {
      method: "GET",
      headers: {
        "Content-Type": "application/json"
      }
    });

    const data = await response.json();

    if (!response.ok || !data.success) {
      document.getElementById("nombreUsuario").textContent = "No se pudo cargar";
      document.getElementById("correoUsuario").textContent = emailGuardado || "Sin correo";
      document.getElementById("carreraUsuario").textContent = "Sin carrera registrada";
      return;
    }

    const usuario = data.data;

    document.getElementById("nombreUsuario").textContent =
      `${usuario.nombre} ${usuario.apellido}`;

    document.getElementById("correoUsuario").textContent =
      usuario.email || emailGuardado || "Sin correo";

    document.getElementById("carreraUsuario").textContent =
      usuario.carrera || "Sin carrera registrada";
  } catch (error) {
    console.error("Error cargando perfil:", error);
    document.getElementById("nombreUsuario").textContent = "Error cargando perfil";
    document.getElementById("correoUsuario").textContent = emailGuardado || "Sin correo";
    document.getElementById("carreraUsuario").textContent = "Sin carrera registrada";
  }
}

async function crearPost() {
  const contenidoInput = document.getElementById("postContenido");
  const message = document.getElementById("postMessage");

  if (!contenidoInput || !message) {
    return;
  }

  const contenido = contenidoInput.value.trim();

  if (!contenido) {
    message.textContent = "Escribe algo antes de publicar.";
    return;
  }

  try {
    const response = await fetch(POSTS_API_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        userId: userId,
        contenido: contenido
      })
    });

    const data = await response.json();

    if (!response.ok || !data.success) {
      message.textContent = data.message || "No se pudo crear el post.";
      return;
    }

    message.textContent = "Publicación creada correctamente.";
    contenidoInput.value = "";
    await cargarPosts();
  } catch (error) {
    console.error("Error creando post:", error);
    message.textContent = "Error de conexión al crear el post.";
  }
}

async function cargarPosts() {
  try {
    const response = await fetch(POSTS_API_URL, {
      method: "GET",
      headers: {
        "Content-Type": "application/json"
      }
    });

    const data = await response.json();
    const container = document.getElementById("postsContainer");

    if (!container) {
      return;
    }

    container.innerHTML = "";

    if (!response.ok || !data.success || !data.data.length) {
      container.innerHTML = `
        <article class="post-card">
          <p>No hay publicaciones todavía.</p>
        </article>
      `;
      return;
    }

    data.data.forEach(post => {
      const article = document.createElement("article");
      article.className = "post-card";

      article.innerHTML = `
        <h3>${post.nombre} ${post.apellido}</h3>
        <p class="post-meta">${post.fechaCreacion}</p>
        <p>${post.contenido}</p>
      `;

      container.appendChild(article);
    });
  } catch (error) {
    console.error("Error cargando posts:", error);
  }
}

function logout() {
  localStorage.removeItem("token");
  localStorage.removeItem("userId");
  localStorage.removeItem("email");
  window.location.href = "login.html";
}

document.addEventListener("DOMContentLoaded", () => {
  const btnCrearPost = document.getElementById("btnCrearPost");

  if (btnCrearPost) {
    btnCrearPost.addEventListener("click", crearPost);
  }

  cargarPerfil();
  cargarPosts();
});