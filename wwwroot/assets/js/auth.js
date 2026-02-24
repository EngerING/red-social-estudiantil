document.getElementById('loginForm').addEventListener('submit', function(e) {
    e.preventDefault();
    
    const btn = document.getElementById('btnLogin');
    btn.classList.add('loading');
    btn.disabled = true;

    // Simulamos una validación de 2 segundos
    setTimeout(() => {
        alert("¡Bienvenido a CampusConnect!");
        btn.classList.remove('loading');
        btn.disabled = false;
        
        // Aquí redirigirías al feed:
        // window.location.href = 'feed.html';
    }, 2000);
});