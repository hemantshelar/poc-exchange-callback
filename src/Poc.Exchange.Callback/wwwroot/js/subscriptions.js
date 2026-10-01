(() => {
  const mailboxEl = document.getElementById("mailbox");
  const urlEl = document.getElementById("notificationUrl");
  const formEl = document.getElementById("create-form");
  const errorEl = document.getElementById("error");
  const fieldsEl = document.getElementById("result-fields");
  const jsonEl = document.getElementById("result-json");
  const rowsEl = document.getElementById("rows");
  const countEl = document.getElementById("list-count");

  function showError(message) {
    errorEl.hidden = !message;
    errorEl.textContent = message ?? "";
  }

  function escapeHtml(value) {
    return (value ?? "").toString()
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  function renderResult(payload) {
    fieldsEl.innerHTML = `
      <div><strong>Subscription id:</strong> ${escapeHtml(payload.id)}</div>
      <div><strong>Mailbox:</strong> ${escapeHtml(payload.mailbox)}</div>
      <div><strong>Resource:</strong> ${escapeHtml(payload.resource)}</div>
      <div><strong>Notification URL:</strong> ${escapeHtml(payload.notificationUrl)}</div>
      <div><strong>Expires:</strong> ${escapeHtml(payload.expirationDateTime)}</div>
      <div><strong>Change type:</strong> ${escapeHtml(payload.changeType)}</div>
    `;
    jsonEl.textContent = JSON.stringify(payload, null, 2);
  }

  function renderList(items) {
    countEl.textContent = `${items.length} subscription${items.length === 1 ? "" : "s"}`;
    if (items.length === 0) {
      rowsEl.innerHTML = `<tr><td class="empty" colspan="5">No subscriptions returned by Graph.</td></tr>`;
      return;
    }

    rowsEl.innerHTML = items.map((item) => `
      <tr>
        <td>${escapeHtml(item.id)}</td>
        <td>${escapeHtml(item.mailbox)}</td>
        <td>${escapeHtml(item.resource)}</td>
        <td>${escapeHtml(item.expirationDateTime)}</td>
        <td>${escapeHtml(item.changeType)}</td>
      </tr>
    `).join("");
  }

  async function readProblem(response) {
    try {
      const body = await response.json();
      return body.detail || body.title || `HTTP ${response.status}`;
    } catch {
      return `HTTP ${response.status}`;
    }
  }

  async function loadSettings() {
    const response = await fetch("/api/settings");
    if (!response.ok) {
      throw new Error(await readProblem(response));
    }
    const settings = await response.json();
    mailboxEl.value = settings.defaultMailbox ?? "";
    urlEl.value = settings.notificationUrl ?? "";
  }

  async function loadList() {
    showError("");
    const response = await fetch("/api/subscriptions");
    if (!response.ok) {
      rowsEl.innerHTML = `<tr><td class="empty" colspan="5">Unable to load subscriptions.</td></tr>`;
      throw new Error(await readProblem(response));
    }
    renderList(await response.json());
  }

  formEl.addEventListener("submit", async (event) => {
    event.preventDefault();
    showError("");
    try {
      const response = await fetch("/api/subscriptions", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mailbox: mailboxEl.value.trim() })
      });
      const payload = await response.json();
      if (!response.ok) {
        jsonEl.textContent = JSON.stringify(payload, null, 2);
        throw new Error(payload.detail || payload.title || `HTTP ${response.status}`);
      }
      renderResult(payload);
      await loadList();
    } catch (error) {
      showError(error.message);
    }
  });

  document.getElementById("refresh").addEventListener("click", async () => {
    try {
      await loadList();
    } catch (error) {
      showError(error.message);
    }
  });

  (async () => {
    try {
      await loadSettings();
      await loadList();
    } catch (error) {
      showError(error.message);
    }
  })();
})();
