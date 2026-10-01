(() => {
  const searchEl = document.getElementById("search");
  const typeEl = document.getElementById("changeType");
  const sortEl = document.getElementById("sort");
  const rowsEl = document.getElementById("rows");
  const countEl = document.getElementById("count");
  const errorEl = document.getElementById("error");

  let items = [];

  function text(value) {
    return (value ?? "").toString();
  }

  function matches(item, query, changeType) {
    if (changeType && text(item.changeType).toLowerCase() !== changeType) {
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
      item.changeType
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
    const filtered = sortItems(
      items.filter((item) => matches(item, query, changeType)),
      sortEl.value
    );

    countEl.textContent = `${filtered.length} of ${items.length} notification${items.length === 1 ? "" : "s"}`;

    if (filtered.length === 0) {
      rowsEl.innerHTML = `<tr><td class="empty" colspan="7">${items.length === 0 ? "No notifications yet." : "No rows match the current filter."}</td></tr>`;
      return;
    }

    rowsEl.innerHTML = filtered.map((item) => `
      <tr>
        <td>${escapeHtml(item.receivedAtUtc)}</td>
        <td>${escapeHtml(item.changeType)}</td>
        <td>${escapeHtml(item.subject)}</td>
        <td>${escapeHtml(item.organizer)}</td>
        <td>${escapeHtml(item.start)}</td>
        <td>${escapeHtml(item.end)}</td>
        <td>${escapeHtml(item.eventId)}</td>
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
      render();
    } catch (error) {
      errorEl.hidden = false;
      errorEl.textContent = `Could not load notifications. ${error.message}`;
      rowsEl.innerHTML = `<tr><td class="empty" colspan="7">Unable to load data.</td></tr>`;
    }
  }

  searchEl.addEventListener("input", render);
  typeEl.addEventListener("change", render);
  sortEl.addEventListener("change", render);
  document.getElementById("refresh").addEventListener("click", load);
  load();
})();
