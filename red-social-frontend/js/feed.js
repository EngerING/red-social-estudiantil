const AUTH_API_URL = "http://localhost:5000/api/Auth";
const POSTS_API_URL = "http://localhost:5000/api/Posts";
const INTERACTIONS_API_URL = "http://localhost:5000/api/Interactions";

const token = localStorage.getItem("token");
const userId = localStorage.getItem("userId");
const emailGuardado = localStorage.getItem("email");

let likedPosts = [];

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

async function cargarLikesDelUsuario() {
  try {
    const response = await fetch(`${INTERACTIONS_API_URL}/liked/${userId}`);
    const data = await response.json();

    if (!response.ok || !data.success) {
      likedPosts = [];
      return;
    }

    likedPosts = data.data || [];
  } catch (error) {
    console.error("Error cargando likes del usuario:", error);
    likedPosts = [];
  }
}

async function crearPost() {
  const contenidoInput = document.getElementById("postContenido");
  const message = document.getElementById("postMessage");

  if (!contenidoInput || !message) return;

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

async function toggleLike(postId) {
  try {
    const response = await fetch(`${INTERACTIONS_API_URL}/toggle-like`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        postId: postId,
        userId: userId
      })
    });

    const data = await response.json();

    if (!response.ok || !data.success) {
      console.error("No se pudo actualizar el like");
      return;
    }

    await cargarPosts();
  } catch (error) {
    console.error("Error haciendo toggle like:", error);
  }
}

async function crearComentario(postId) {
  const input = document.getElementById(`commentInput-${postId}`);
  if (!input) return;

  const contenido = input.value.trim();
  if (!contenido) return;

  try {
    const response = await fetch(`${INTERACTIONS_API_URL}/comment`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        postId: postId,
        userId: userId,
        contenido: contenido
      })
    });

    const data = await response.json();

    if (!response.ok || !data.success) {
      console.error("No se pudo crear el comentario");
      return;
    }

    input.value = "";
    await cargarComentarios(postId);
  } catch (error) {
    console.error("Error creando comentario:", error);
  }
}

async function cargarComentarios(postId) {
  try {
    const response = await fetch(`${INTERACTIONS_API_URL}/comments/${postId}`);
    const data = await response.json();
    const container = document.getElementById(`comments-${postId}`);

    if (!container) return;

    container.innerHTML = "";

    if (!response.ok || !data.success || !data.data.length) {
      container.innerHTML = `<p class="no-comments">No hay comentarios todavía.</p>`;
      return;
    }

    data.data.forEach(comentario => {
      const item = document.createElement("div");
      item.className = "comment-item";

      item.innerHTML = `
        <strong>${comentario.nombre} ${comentario.apellido}</strong>
        <p>${comentario.contenido}</p>
        <p class="comment-date">${comentario.fechaCreacion}</p>
      `;

      container.appendChild(item);
    });
  } catch (error) {
    console.error("Error cargando comentarios:", error);
  }
}

async function cargarPosts() {
  try {
    await cargarLikesDelUsuario();

    const response = await fetch(POSTS_API_URL);
    const data = await response.json();
    const container = document.getElementById("postsContainer");

    if (!container) return;

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
      const yaDioLike = likedPosts.includes(post.id);

      const article = document.createElement("article");
      article.className = "post-card";

      article.innerHTML = `
        <h3>${post.nombre} ${post.apellido}</h3>
        <p class="post-meta">${post.fechaCreacion}</p>
        <p>${post.contenido}</p>

        <div class="post-actions">
          <button class="like-btn" data-post-id="${post.id}">
            ${yaDioLike ? "Quitar like" : "Like"}
          </button>
          <span class="likes-count">Likes: ${post.likesCount}</span>
        </div>

        <div class="comment-box">
          <input
            type="text"
            id="commentInput-${post.id}"
            class="comment-input"
            placeholder="Escribe un comentario..."
          />
          <button class="comment-btn" data-post-id="${post.id}">Comentar</button>
        </div>

        <div class="comments-list" id="comments-${post.id}">
          <p class="no-comments">Cargando comentarios...</p>
        </div>
      `;

      container.appendChild(article);
    });

    document.querySelectorAll(".like-btn").forEach(button => {
      button.addEventListener("click", () => {
        const postId = button.getAttribute("data-post-id");
        toggleLike(postId);
      });
    });

    document.querySelectorAll(".comment-btn").forEach(button => {
      button.addEventListener("click", () => {
        const postId = button.getAttribute("data-post-id");
        crearComentario(postId);
      });
    });

    for (const post of data.data) {
      await cargarComentarios(post.id);
    }
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