/* La interfaz solo consume la API. No usa localStorage ni sessionStorage. */
(() => {
  "use strict";
  // Retira una vez la clave demo creada por la versión anterior.
  try { localStorage.removeItem("gestionReservas.data.v1"); } catch { /* El modo remoto no depende del almacenamiento del navegador. */ }
  const data = { version: 1, tables: [], dishes: [], reservations: [], orders: [], deliveries: [], users: [] };
  const localDate = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`; };
  async function request(path, options = {}) {
    const response = await fetch(`/api${path}`, {
      ...options,
      credentials: "same-origin",
      headers: { ...(options.body ? { "Content-Type": "application/json" } : {}), ...(options.headers || {}) }
    });
    if (response.status === 204) return null;
    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.error || (response.status === 401 ? "Inicia sesión para continuar." : `Error del servidor (${response.status}).`));
    return result;
  }
  async function refresh() { Object.assign(data, await request("/bootstrap")); return data; }
  const json = (value) => JSON.stringify(value);
  window.Store = {
    get: () => data,
    init: refresh,
    async refresh() { return refresh(); },
    async reset() { await request("/reset-demo", { method: "POST" }); return refresh(); },
    getToday: localDate,
    async createReservation(input) { const reservation = await request("/reservations", { method: "POST", body: json({ ...input, tableId: Number(input.tableId), people: Number(input.people), time: `${input.time}:00` }) }); await refresh(); return reservation; },
    async updateReservation(id, status) { await request(`/reservations/${encodeURIComponent(id)}/status`, { method: "PATCH", body: json({ status }) }); await refresh(); },
    async createOrder(input, creator) { const order = await request("/orders", { method: "POST", body: json({ ...input, tableId: Number(input.tableId), createdBy: creator, lines: input.lines.filter((line) => Number(line.quantity) > 0).map((line) => ({ dishId: line.dishId, quantity: Number(line.quantity) })) }) }); await refresh(); return order; },
    async updateLine(orderId, lineIndex, status) { await request(`/orders/${encodeURIComponent(orderId)}/lines/${lineIndex}`, { method: "PATCH", body: json({ status }) }); await refresh(); },
    async createDelivery(orderId) { const delivery = await request("/deliveries", { method: "POST", body: json({ orderId }) }); await refresh(); return delivery; },
    async updateDelivery(id, status) { await request(`/deliveries/${encodeURIComponent(id)}/status`, { method: "PATCH", body: json({ status }) }); await refresh(); },
    async addUser(input) { const user = await request("/users", { method: "POST", body: json(input) }); await refresh(); return user; },
    async removeUser(id) { await request(`/users/${encodeURIComponent(id)}`, { method: "DELETE" }); await refresh(); }
  };
})();
