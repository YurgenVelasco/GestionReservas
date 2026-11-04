(() => {
  "use strict";
  const ACCESS = {
    admin: ["dashboard", "reservas", "pedidos", "cocina", "despachos", "mesas", "usuarios"],
    mesero: ["dashboard", "reservas", "pedidos", "despachos", "mesas"],
    cocina: ["dashboard", "cocina"],
    despacho: ["dashboard", "despachos"]
  };
  let current = null;
  async function readUser(response) { if (!response.ok) return null; return response.json(); }
  window.Auth = {
    async login(username, password) {
      let response;
      try { response = await fetch("/api/auth/login", { method: "POST", credentials: "same-origin", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username, password }) }); }
      catch { throw new Error("No hay conexión con el servidor. Inicia la aplicación con Iniciar-MesaComun.bat."); }
      if (response.status === 401) throw new Error("Usuario o contraseña incorrectos.");
      if (!response.ok) throw new Error("No se pudo conectar con la base de datos local.");
      current = await response.json(); return current;
    },
    async restore() {
      try { current = await readUser(await fetch("/api/me", { credentials: "same-origin" })); }
      catch { current = null; }
      return current;
    },
    async logout() {
      try { await fetch("/api/auth/logout", { method: "POST", credentials: "same-origin" }); } finally { current = null; }
    },
    user: () => current,
    can(view) { return !!current && (ACCESS[current.role] || []).includes(view); },
    menu: () => [{ id: "dashboard", label: "Resumen", icon: "⌂", section: "GENERAL" }, { id: "reservas", label: "Reservas", icon: "▦" }, { id: "pedidos", label: "Pedidos", icon: "◫", section: "OPERACIÓN" }, { id: "cocina", label: "Cocina", icon: "♨" }, { id: "despachos", label: "Despachos", icon: "➜" }, { id: "mesas", label: "Mesas", icon: "▤", section: "RESTAURANTE" }, { id: "usuarios", label: "Usuarios", icon: "♙", section: "ADMINISTRACIÓN" }].filter((item) => current && (ACCESS[current.role] || []).includes(item.id))
  };
})();
