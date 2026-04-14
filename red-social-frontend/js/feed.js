const API_BASE = "http://localhost:5000/api";
const HUB_URL = "http://localhost:5000/hubs/chat";

const token = localStorage.getItem("token");
const userId = localStorage.getItem("userId");
const emailGuardado = localStorage.getItem("email");

let likedPosts = [];
let chatConnection = null;

if (!token || !userId) {
  window.location.href = "login.html";
}

async function cargarPerfil() {
  try {
    const response = await fetch(`${API_BASE}/Auth/profile/${userId}`);
    const data = await response.json();

    if (!response.ok || !data.success) return;

    const usuario = data.data;
    document.getElementById("nombreUsuario").textContent = `${usuario.nombre} ${usuario.apellido}`;
    document.getElementById("correoUsuario").textContent = usuario.email || emailGuardado || "Sin correo";
    document.getElementById("carreraUsuario").textContent = usuario.carrera || "Sin carrera registrada";

    if (usuario.fotoPerfilUrl) {
      document.getElementById("fotoPerfil").src = usuario.fotoPerfilUrl;
    }
  } catch (error) {
    console.error(error);
  }
}

async function subirFotoPerfil() {
  const input = document.getElementById("fotoInput");
  if (!input.files.length) return;

  const formData = new FormData();
  formData.append("foto", input.files[0]);

  const response = await fetch(`${API_BASE}/Auth/profile/${userId}/photo`, {
    method: "POST",
    body: formData
  });

  const data = await response.json();
  if (response.ok && data.success && data.fotoPerfilUrl) {
    document.getElementById("fotoPerfil").src = data.fotoPerfilUrl;
  }
}

async function cargarLikesDelUsuario() {
  const response = await fetch(`${API_BASE}/Interactions/liked/${userId}`);
  const data = await response.json();
  likedPosts = response.ok && data.success ? data.data || [] : [];
}

async function crearPost() {
  const contenidoInput = document.getElementById("postContenido");
  const contenido = contenidoInput.value.trim();
  if (!contenido) return;

  const response = await fetch(`${API_BASE}/Posts`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, contenido })
  });

  const data = await response.json();
  document.getElementById("postMessage").textContent = data.message || "Operación completada";

  if (response.ok && data.success) {
    contenidoInput.value = "";
    await cargarPosts();
  }
}

async function editarPost(postId, contenidoActual) {
  const nuevo = prompt("Editar publicación:", contenidoActual);
  if (!nuevo || !nuevo.trim()) return;

  await fetch(`${API_BASE}/Posts/${postId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, contenido: nuevo.trim() })
  });

  await cargarPosts();
}

async function eliminarPost(postId) {
  if (!confirm("¿Eliminar publicación?")) return;

  await fetch(`${API_BASE}/Posts/${postId}`, {
    method: "DELETE",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId })
  });

  await cargarPosts();
}

async function toggleLike(postId) {
  await fetch(`${API_BASE}/Interactions/toggle-like`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ postId, userId })
  });
  await cargarPosts();
}

async function crearComentario(postId) {
  const input = document.getElementById(`commentInput-${postId}`);
  const contenido = input.value.trim();
  if (!contenido) return;

  await fetch(`${API_BASE}/Interactions/comment`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ postId, userId, contenido })
  });

  input.value = "";
  await cargarComentarios(postId);
}

async function editarComentario(comentarioId, postId, contenidoActual) {
  const nuevo = prompt("Editar comentario:", contenidoActual);
  if (!nuevo || !nuevo.trim()) return;

  await fetch(`${API_BASE}/Interactions/comment/${comentarioId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, contenido: nuevo.trim() })
  });

  await cargarComentarios(postId);
}

async function eliminarComentario(comentarioId, postId) {
  if (!confirm("¿Eliminar comentario?")) return;

  await fetch(`${API_BASE}/Interactions/comment/${comentarioId}`, {
    method: "DELETE",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId })
  });

  await cargarComentarios(postId);
}

async function cargarComentarios(postId) {
  const response = await fetch(`${API_BASE}/Interactions/comments/${postId}`);
  const data = await response.json();
  const container = document.getElementById(`comments-${postId}`);
  container.innerHTML = "";

  const comentarios = response.ok && data.success ? data.data || [] : [];
  if (!comentarios.length) {
    container.innerHTML = `<p class="no-comments">No hay comentarios todavía.</p>`;
    return;
  }

  comentarios.forEach(comentario => {
    const acciones = comentario.userId === userId
      ? `<div class="row-actions">
          <button class="mini-btn edit-comment-btn" data-comment-id="${comentario.id}" data-post-id="${postId}" data-content="${encodeURIComponent(comentario.contenido)}">Editar</button>
          <button class="mini-btn danger delete-comment-btn" data-comment-id="${comentario.id}" data-post-id="${postId}">Eliminar</button>
        </div>`
      : "";

    const item = document.createElement("div");
    item.className = "comment-item";
    item.innerHTML = `
      <strong>${comentario.nombre} ${comentario.apellido}</strong>
      <p>${comentario.contenido}</p>
      <p class="comment-date">${comentario.fechaCreacion}</p>
      ${acciones}
    `;

    container.appendChild(item);
  });
}

async function cargarPosts() {
  await cargarLikesDelUsuario();

  const response = await fetch(`${API_BASE}/Posts`);
  const data = await response.json();
  const container = document.getElementById("postsContainer");
  container.innerHTML = "";

  const posts = response.ok && data.success ? data.data || [] : [];
  if (!posts.length) {
    container.innerHTML = `<article class="post-card"><p>No hay publicaciones todavía.</p></article>`;
    return;
  }

  posts.forEach(post => {
    const yaDioLike = likedPosts.includes(post.id);
    const accionesPost = post.userId === userId
      ? `<div class="row-actions">
          <button class="mini-btn edit-post-btn" data-post-id="${post.id}" data-content="${encodeURIComponent(post.contenido)}">Editar</button>
          <button class="mini-btn danger delete-post-btn" data-post-id="${post.id}">Eliminar</button>
        </div>`
      : "";

    const article = document.createElement("article");
    article.className = "post-card";
    article.innerHTML = `
      <h3>${post.nombre} ${post.apellido}</h3>
      <p class="post-meta">${post.fechaCreacion}</p>
      <p>${post.contenido}</p>
      ${accionesPost}
      <div class="post-actions">
        <button class="like-btn" data-post-id="${post.id}">${yaDioLike ? "Quitar like" : "Like"}</button>
        <span class="likes-count">Likes: ${post.likesCount}</span>
      </div>
      <div class="comment-box">
        <input type="text" id="commentInput-${post.id}" class="comment-input" placeholder="Escribe un comentario..." />
        <button class="comment-btn" data-post-id="${post.id}">Comentar</button>
      </div>
      <div class="comments-list" id="comments-${post.id}"><p class="no-comments">Cargando comentarios...</p></div>
    `;
    container.appendChild(article);
  });

  document.querySelectorAll(".like-btn").forEach(button => button.addEventListener("click", () => toggleLike(button.dataset.postId)));
  document.querySelectorAll(".comment-btn").forEach(button => button.addEventListener("click", () => crearComentario(button.dataset.postId)));
  document.querySelectorAll(".edit-post-btn").forEach(button => button.addEventListener("click", () => editarPost(button.dataset.postId, decodeURIComponent(button.dataset.content || ""))));
  document.querySelectorAll(".delete-post-btn").forEach(button => button.addEventListener("click", () => eliminarPost(button.dataset.postId)));
  document.querySelectorAll(".edit-comment-btn").forEach(button => button.addEventListener("click", () => editarComentario(button.dataset.commentId, button.dataset.postId, decodeURIComponent(button.dataset.content || ""))));
  document.querySelectorAll(".delete-comment-btn").forEach(button => button.addEventListener("click", () => eliminarComentario(button.dataset.commentId, button.dataset.postId)));

  for (const post of posts) {
    await cargarComentarios(post.id);
  }
}

function appendChatMessage(message) {
  const container = document.getElementById("chatMessages");
  const item = document.createElement("div");
  item.className = "chat-message";
  item.innerHTML = `<strong>${message.nombreCompleto}</strong><p>${message.contenido}</p><span>${message.fechaCreacion}</span>`;
  container.appendChild(item);
  container.scrollTop = container.scrollHeight;
}

async function cargarMensajesChat() {
  const response = await fetch(`${API_BASE}/Interactions/chat/messages`);
  const data = await response.json();
  const mensajes = response.ok && data.success ? data.data || [] : [];
  document.getElementById("chatMessages").innerHTML = "";
  mensajes.forEach(appendChatMessage);
}

async function iniciarChat() {
  await cargarMensajesChat();

  chatConnection = new signalR.HubConnectionBuilder().withUrl(HUB_URL).withAutomaticReconnect().build();

  chatConnection.on("ReceiveMessage", (message) => {
    appendChatMessage(message);
  });

  await chatConnection.start();
}

async function enviarMensajeChat() {
  const input = document.getElementById("chatInput");
  const contenido = input.value.trim();
  if (!contenido || !chatConnection) return;

  await chatConnection.invoke("SendMessage", { userId, contenido });
  input.value = "";
}

function logout() {
  localStorage.removeItem("token");
  localStorage.removeItem("userId");
  localStorage.removeItem("email");
  window.location.href = "login.html";
}

document.addEventListener("DOMContentLoaded", async () => {
  document.getElementById("btnCrearPost")?.addEventListener("click", crearPost);
  document.getElementById("btnSubirFoto")?.addEventListener("click", subirFotoPerfil);
  document.getElementById("btnSendChat")?.addEventListener("click", enviarMensajeChat);

  await cargarPerfil();
  await cargarPosts();
  await iniciarChat();
});
