(() => {
  const searchEl = document.getElementById("search");
  const mailboxEl = document.getElementById("sourceMailbox");
  const typeEl = document.getElementById("changeType");
  const sortEl = document.getElementById("sort");
  const rowsEl = document.getElementById("rows");
  const countEl = document.getElementById("count");
  const errorEl = document.getElementById("error");

  let items = [];

  function text(value) {
    return (value ?? "").toString();
  }

  function fillMailboxFilter() {
    const selected = mailboxEl.value;
    const mailboxes = [...new Set(items.map((item) => text(item.sourceMailbox)).filter(Boolean))].sort();
    mailboxEl.innerHTML = `<option value="">All mailboxes</option>` +
      mailboxes.map((mailbox) => `<option value="${escapeHtml(mailbox)}">${escapeHtml(mailbox)}</option>`).join("");
    if (mailboxes.includes(selected)) {
      mailboxEl.value = selected;
    }
  }

  function matches(item, query, changeType, mailbox) {
    if (changeType && text(item.changeType).toLowerCase() !== changeType) {
      return false;
    }
    if (mailbox && text(item.sourceMailbox).toLowerCase() !== mailbox.toLowerCase()) {
      return false;
    }
    if (!query) {
      return true;
    }
    const haystack = [
      item.subject,
      item.organizer,
      item.eventId,
      item.detail,
      item.changeType,
      item.sourceMailbox,
      item.subscriptionId,
      item.attendeeResponses
    ].map((part) => text(part).toLowerCase()).join(" ");
    return haystack.includes(query);
  }

  function sortItems(list, sort) {
    const copy = [...list];
    copy.sort((a, b) => {
      if (sort === "receivedAsc") {
        return Date.parse(a.receivedAtUtc) - Date.parse(b.receivedAtUtc);
      }
      if (sort === "subject") {
        return text(a.subject).localeCompare(text(b.subject));
      }
      if (sort === "changeType") {
        return text(a.changeType).localeCompare(text(b.changeType));
      }
      return Date.parse(b.receivedAtUtc) - Date.parse(a.receivedAtUtc);
    });
    return copy;
  }

  function render() {
    const query = searchEl.value.trim().toLowerCase();
    const changeType = typeEl.value.toLowerCase();
    const mailbox = mailboxEl.value;
    const filtered = sortItems(
      items.filter((item) => matches(item, query, changeType, mailbox)),
      sortEl.value
    );

    countEl.textContent = `${filtered.length} of ${items.length} notification${items.length === 1 ? "" : "s"}`;

    if (filtered.length === 0) {
      rowsEl.innerHTML = `<tr><td class="empty" colspan="8">${items.length === 0 ? "No notifications yet." : "No rows match the current filter."}</td></tr>`;
      return;
    }

    rowsEl.innerHTML = filtered.map((item) => `
      <tr>
        <td>${escapeHtml(item.receivedAtUtc)}</td>
        <td>${escapeHtml(item.sourceMailbox)}</td>
        <td>${escapeHtml(item.changeType)}</td>
        <td>${escapeHtml(item.subject)}</td>
        <td>${escapeHtml(item.organizer)}</td>
        <td>${escapeHtml(item.attendeeResponses)}</td>
        <td>${escapeHtml(item.start)}</td>
        <td title="${escapeHtml(item.subscriptionId)}">${escapeHtml(item.eventId)}</td>
      </tr>
    `).join("");
  }

  function escapeHtml(value) {
    return text(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  async function load() {
    errorEl.hidden = true;
    try {
      const response = await fetch("/api/notifications");
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      items = await response.json();
      fillMailboxFilter();
      render();
    } catch (error) {
      errorEl.hidden = false;
      errorEl.textContent = `Could not load notifications. ${error.message}`;
      rowsEl.innerHTML = `<tr><td class="empty" colspan="8">Unable to load data.</td></tr>`;
    }
  }

  searchEl.addEventListener("input", render);
  mailboxEl.addEventListener("change", render);
  typeEl.addEventListener("change", render);
  sortEl.addEventListener("change", render);
  document.getElementById("refresh").addEventListener("click", load);
  load();
})();
