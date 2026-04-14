const API_BASE = "http://localhost:5000/api";

const token = localStorage.getItem("token");
const userId = localStorage.getItem("userId");
const emailGuardado = localStorage.getItem("email");

let fotoPerfilOriginal = "https://via.placeholder.com/180?text=%F0%9F%91%A4";
let fotoPreviewUrl = "";

if (!token || !userId) {
  window.location.href = "login.html";
}

function bindMenuNavigation() {
  document.querySelectorAll("[data-link]").forEach((button) => {
    button.addEventListener("click", () => {
      const link = button.dataset.link;
      if (link) {
        window.location.href = link;
      }
    });
  });
}

function setProfileMessage(message, isError = false) {
  const node = document.getElementById("profileMessage");
  if (!node) return;

  node.textContent = message;
  node.style.color = isError ? "#b33b3b" : "#4b568f";
}

function escapeHtml(value = "") {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function shortenText(value = "", maxLength = 18) {
  if (value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength - 1)}…`;
}

async function parseResponse(response) {
  try {
    return await response.json();
  } catch {
    return { success: false, message: "Respuesta no valida del servidor" };
  }
}

async function cargarPerfil() {
  try {
    const response = await fetch(`${API_BASE}/Auth/profile/${userId}`);
    const data = await parseResponse(response);

    if (!response.ok || !data.success) {
      setProfileMessage(data.message || "No se pudo cargar el perfil.", true);
      return;
    }

    const usuario = data.data;
    const fullName = `${usuario.nombre || ""} ${usuario.apellido || ""}`.trim();
    const handle = `@${(usuario.email || emailGuardado || "campusconnect").split("@")[0].toLowerCase()}`;

    document.getElementById("nombreUsuario").textContent = fullName || "Usuario";
    document.getElementById("usuarioHandle").textContent = handle;
    document.getElementById("correoUsuario").textContent = usuario.email || emailGuardado || "Sin correo";
    document.getElementById("carreraUsuario").textContent = usuario.carrera || "Sin carrera registrada";
    document.getElementById("statsCarrera").textContent = shortenText(usuario.carrera || "Sin carrera");
    renderProfileGroups(usuario.studyGroupNames || []);

    if (usuario.fotoPerfilUrl) {
      fotoPerfilOriginal = usuario.fotoPerfilUrl;
    }

    document.getElementById("fotoPerfil").src = fotoPerfilOriginal;
  } catch (error) {
    console.error(error);
    setProfileMessage("Error de conexion al cargar el perfil.", true);
  }
}

function renderProfileGroups(groups) {
  const container = document.getElementById("profileGroups");
  if (!container) return;

  if (!groups.length) {
    container.innerHTML = `<span class="group-pill muted-pill">Todavía no perteneces a ningún grupo</span>`;
    return;
  }

  container.innerHTML = groups
    .map((group) => `<span class="group-pill">${escapeHtml(group)}</span>`)
    .join("");
}

async function cargarMisPosts() {
  const container = document.getElementById("misPostsContainer");
  if (!container) return;

  try {
    const response = await fetch(`${API_BASE}/Posts`);
    const data = await parseResponse(response);

    if (!response.ok || !data.success) {
      container.innerHTML = `<article class="empty-posts">No se pudieron cargar tus publicaciones.</article>`;
      return;
    }

    const posts = (data.data || []).filter((post) => post.userId === userId);
    document.getElementById("statsPosts").textContent = String(posts.length);

    if (!posts.length) {
      container.innerHTML = `<article class="empty-posts">Aún no has publicado nada. Tu perfil está listo para tu primera publicación.</article>`;
      return;
    }

    container.innerHTML = posts.map((post) => `
      <article class="profile-post-card">
        <div>
          <h4>${escapeHtml(post.nombre || "Publicación")}</h4>
          <p>${escapeHtml(post.contenido || "")}</p>
        </div>
        <div class="profile-post-meta">
          <span>${escapeHtml(post.fechaCreacion || "")}</span>
          <span>${post.likesCount || 0} likes</span>
        </div>
      </article>
    `).join("");
  } catch (error) {
    console.error(error);
    container.innerHTML = `<article class="empty-posts">No se pudieron cargar tus publicaciones.</article>`;
  }
}

async function subirFotoPerfil() {
  const input = document.getElementById("fotoInput");
  if (!input?.files.length) return;

  const formData = new FormData();
  formData.append("foto", input.files[0]);
  setProfileMessage("Actualizando foto...");

  try {
    const response = await fetch(`${API_BASE}/Auth/profile/${userId}/photo`, {
      method: "POST",
      body: formData
    });

    const data = await parseResponse(response);
    if (!response.ok || !data.success || !data.fotoPerfilUrl) {
      setProfileMessage(data.message || "No se pudo actualizar la foto.", true);
      return;
    }

    fotoPerfilOriginal = data.fotoPerfilUrl;
    document.getElementById("fotoPerfil").src = data.fotoPerfilUrl;

    if (fotoPreviewUrl) {
      URL.revokeObjectURL(fotoPreviewUrl);
      fotoPreviewUrl = "";
    }

    setProfileMessage("Foto actualizada correctamente.");
  } catch (error) {
    console.error(error);
    setProfileMessage("Ocurrió un error al actualizar la foto.", true);
  }
}

function manejarSeleccionDeFoto(event) {
  const input = event.target;
  const label = document.getElementById("fotoSeleccionada");
  const avatar = document.getElementById("fotoPerfil");
  const file = input?.files?.[0];

  if (label) {
    label.textContent = file ? file.name : "Ningún archivo seleccionado";
  }

  if (!avatar) return;

  if (fotoPreviewUrl) {
    URL.revokeObjectURL(fotoPreviewUrl);
    fotoPreviewUrl = "";
  }

  if (!file) {
    avatar.src = fotoPerfilOriginal;
    return;
  }

  fotoPreviewUrl = URL.createObjectURL(file);
  avatar.src = fotoPreviewUrl;
}

function logout() {
  localStorage.removeItem("token");
  localStorage.removeItem("userId");
  localStorage.removeItem("email");
  window.location.href = "login.html";
}

document.addEventListener("DOMContentLoaded", async () => {
  bindMenuNavigation();
  document.getElementById("btnCambiarFoto")?.addEventListener("click", () => {
    document.getElementById("fotoInput")?.click();
  });
  document.getElementById("fotoInput")?.addEventListener("change", manejarSeleccionDeFoto);
  document.getElementById("btnSubirFoto")?.addEventListener("click", subirFotoPerfil);
  document.getElementById("btnLogout")?.addEventListener("click", logout);

  await cargarPerfil();
  await cargarMisPosts();
});
