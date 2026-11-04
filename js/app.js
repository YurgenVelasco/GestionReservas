(() => {
  "use strict";
  const $ = (selector, root = document) => root.querySelector(selector);
  const viewNames = { dashboard: ["RESUMEN", "Panel"], reservas: ["OPERACIÓN", "Reservas"], pedidos: ["OPERACIÓN", "Pedidos"], cocina: ["OPERACIÓN", "Cocina"], despachos: ["OPERACIÓN", "Despachos"], mesas: ["RESTAURANTE", "Mesas"], usuarios: ["ADMINISTRACIÓN", "Usuarios"] };
  let activeView = "dashboard", toastTimer;
  const roleLabels = { admin: "Administrador", mesero: "Mesero", cocina: "Cocina", despacho: "Despacho" };
  function toast(message) { const node = $("#toast"); node.textContent = message; node.classList.add("show"); clearTimeout(toastTimer); toastTimer = setTimeout(() => node.classList.remove("show"), 2600); }
  async function showApp() {
    const user = Auth.user(); if (!user) return;
    await Store.init();
    $("#loginScreen").classList.add("hidden"); $("#appShell").classList.remove("hidden");
    $("#profileName").textContent = user.name; $("#profileRole").textContent = roleLabels[user.role] || user.role; $("#avatar").textContent = user.name.slice(0, 1).toUpperCase();
    $("#todayLabel").textContent = new Intl.DateTimeFormat("es-CO", { weekday: "long", day: "numeric", month: "long", year: "numeric" }).format(new Date());
    renderNavigation(); renderView("dashboard");
  }
  function renderNavigation() {
    let lastSection = "";
    $("#navigation").innerHTML = Auth.menu().map((item) => { const label = item.section && item.section !== lastSection ? `<div class="nav-label">${item.section}</div>` : ""; if (item.section) lastSection = item.section; return `${label}<a class="nav-link ${activeView === item.id ? "active" : ""}" href="#${item.id}" data-view="${item.id}"><span class="nav-icon">${item.icon}</span>${item.label}</a>`; }).join("");
  }
  function renderView(view) {
    if (!Auth.can(view) || !Views[view]) view = "dashboard";
    activeView = view; const details = viewNames[view]; $("#pageEyebrow").textContent = details[0]; $("#pageTitle").textContent = details[1]; $("#viewRoot").innerHTML = Views[view](); renderNavigation(); $("#sidebar").classList.remove("open");
  }
  function openModal(title, subtitle, fields, submitLabel = "Guardar") {
    $("#modal").innerHTML = `<div class="modal-head"><div><h2 id="modalTitle">${title}</h2><p>${subtitle}</p></div><button class="close-modal" type="button" data-action="close-modal" aria-label="Cerrar">×</button></div><form id="dataForm">${fields}<div class="modal-actions"><button class="button" type="button" data-action="close-modal">Cancelar</button><button class="button primary" type="submit">${submitLabel}</button></div><p class="login-error" id="formError" role="alert"></p></form>`;
    $("#modalBackdrop").classList.remove("hidden"); const first = $("#dataForm input, #dataForm select"); if (first) first.focus();
  }
  function field(label, name, type = "text", extra = "", required = true) { return `<div class="field"><label for="f_${name}">${label}</label><input id="f_${name}" name="${name}" type="${type}" ${required ? "required" : ""} ${extra}></div>`; }
  function newReservation() {
    const tables = Store.get().tables.map((table) => `<option value="${table.id}">${table.name} · ${table.seats} puestos</option>`).join("");
    openModal("Nueva reserva", "Completa los datos para apartar una mesa.", `<div class="form-grid">${field("Nombre del cliente", "name")}${field("Teléfono", "phone", "tel", "maxlength=30", false)}${field("Fecha", "date", "date", `min="${Store.getToday()}"`)}${field("Hora", "time", "time")}<div class="field"><label for="f_tableId">Mesa</label><select id="f_tableId" name="tableId" required>${tables}</select></div>${field("Personas", "people", "number", "min=1 max=8 value=2")}</div>`);
  }
  function newOrder() {
    const tables = Store.get().tables.map((table) => `<option value="${table.id}">${table.name}</option>`).join("");
    const dishes = Store.get().dishes.map((dish) => `<div class="field"><label for="dish_${dish.id}">${dish.name} · ${Views.money(dish.price)}</label><input id="dish_${dish.id}" name="dish_${dish.id}" type="number" min="0" max="20" value="0"></div>`).join("");
    openModal("Crear pedido", "Elige la mesa y las cantidades de cada plato.", `<div class="form-grid"><div class="field full"><label for="f_tableId">Mesa</label><select id="f_tableId" name="tableId" required>${tables}</select></div>${dishes}<div class="field full"><label for="f_notes">Notas para cocina</label><input id="f_notes" name="notes" maxlength="160" placeholder="Opcional"></div></div>`);
  }
  function newUser() { openModal("Agregar perfil", "La cuenta quedará guardada en SQL Server.", `<div class="form-grid">${field("Nombre", "name")} ${field("Nombre de usuario", "username", "text", "minlength=3 maxlength=30 pattern=[a-zA-Z0-9._-]+")}${field("Contraseña", "password", "password", "minlength=10 autocomplete=new-password")}<div class="field"><label for="f_role">Rol</label><select id="f_role" name="role"><option value="mesero">Mesero</option><option value="cocina">Cocina</option><option value="despacho">Despacho</option><option value="admin">Administrador</option></select></div></div>`, "Crear perfil"); }
  function formData(form) { return Object.fromEntries(new FormData(form).entries()); }
  fetch("/api/health").then((response) => { if (!response.ok) throw new Error(); return response.json(); }).then(() => { const status = $("#databaseStatus"); status.classList.add("connected"); status.innerHTML = "<i></i><span>Conectado a SQL Server LocalDB</span>"; }).catch(() => { const status = $("#databaseStatus"); status.classList.add("disconnected"); status.innerHTML = "<i></i><span>Servidor sin conexión · ejecuta Iniciar-MesaComun.bat</span>"; });
  $("#loginForm").addEventListener("submit", async (event) => { event.preventDefault(); try { const form = formData(event.currentTarget); await Auth.login(form.username, form.password); await showApp(); $("#loginError").textContent = ""; } catch (error) { $("#loginError").textContent = error.message; } });
  $("#logoutButton").addEventListener("click", async () => { await Auth.logout(); $("#appShell").classList.add("hidden"); $("#loginScreen").classList.remove("hidden"); $("#loginForm").reset(); });
  $("#menuToggle").addEventListener("click", () => $("#sidebar").classList.toggle("open"));
  document.addEventListener("click", async (event) => {
    const nav = event.target.closest("[data-view]"); if (nav) { event.preventDefault(); renderView(nav.dataset.view); return; }
    const control = event.target.closest("[data-action]"); if (!control) return;
    const action = control.dataset.action;
    try {
      if (action === "new-reservation") newReservation();
      else if (action === "new-order") newOrder();
      else if (action === "new-user") newUser();
      else if (action === "close-modal") $("#modalBackdrop").classList.add("hidden");
      else if (action === "reservation-status") { await Store.updateReservation(control.dataset.id, control.dataset.status); toast("Reserva actualizada."); renderView(activeView); }
      else if (action === "line-status") { const holder = control.parentElement.querySelector("[data-order]"); await Store.updateLine(holder.dataset.order, holder.dataset.index, holder.dataset.current === "pendiente" ? "preparacion" : "listo"); toast("Estado del plato actualizado."); renderView(activeView); }
      else if (action === "create-delivery") { await Store.createDelivery(control.dataset.order); toast("Despacho creado."); renderView(activeView); }
      else if (action === "delivery-status") { const holder = control.parentElement.querySelector("[data-delivery]"); await Store.updateDelivery(holder.dataset.delivery, holder.dataset.current === "pendiente" ? "ruta" : "entregado"); toast("Despacho actualizado."); renderView(activeView); }
      else if (action === "reset-data") { if (window.confirm("¿Restablecer reservas, pedidos, despachos y perfiles personalizados en SQL Server? Esta acción no se puede deshacer.")) { await Store.reset(); renderView(activeView); toast("Datos restablecidos en LocalDB."); } }
      else if (action === "remove-user") { const holder = control.parentElement.querySelector("[data-user]"); await Store.removeUser(holder.dataset.user); renderView(activeView); toast("Perfil eliminado."); }
    } catch (error) { toast(error.message); }
  });
  $("#modalBackdrop").addEventListener("click", (event) => { if (event.target.id === "modalBackdrop") $("#modalBackdrop").classList.add("hidden"); });
  document.addEventListener("keydown", (event) => { if (event.key === "Escape") $("#modalBackdrop").classList.add("hidden"); });
  $("#modal").addEventListener("submit", async (event) => {
    if (event.target.id !== "dataForm") return; event.preventDefault(); const form = formData(event.target);
    try {
      if (activeView === "reservas" || $("#modalTitle").textContent === "Nueva reserva") await Store.createReservation(form, Auth.user().username);
      else if ($("#modalTitle").textContent === "Crear pedido") { const lines = Store.get().dishes.map((dish) => ({ dishId: dish.id, quantity: Number(form[`dish_${dish.id}`] || 0) })); await Store.createOrder({ ...form, lines }, Auth.user().username); }
      else await Store.addUser(form);
      $("#modalBackdrop").classList.add("hidden"); toast("Guardado correctamente."); renderView(activeView);
    } catch (error) { $("#formError").textContent = error.message; }
  });
  (async () => { const user = await Auth.restore(); if (user) await showApp(); })().catch((error) => { $("#loginError").textContent = error.message; });
})();
