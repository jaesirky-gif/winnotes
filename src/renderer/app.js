(function bootstrap() {
  const SMART_ITEMS = [
    { id: "all-notes", name: "全部备忘录", hint: "所有文件夹" },
    { id: "pinned-notes", name: "已置顶", hint: "重要内容" }
  ];

  const state = {
    meta: null,
    searchQuery: "",
    store: null,
    unsubscribeCommand: null
  };

  const dom = {};

  const persistStore = debounce(async () => {
    if (!state.store) {
      return;
    }

    await window.notesAPI.saveStore(structuredClone(state.store));
  }, 220);

  document.addEventListener("DOMContentLoaded", init);
  window.addEventListener("beforeunload", teardown);

  async function init() {
    cacheDom();
    bindEvents();

    const [meta, loadedStore] = await Promise.all([window.notesAPI.getMeta(), window.notesAPI.loadStore()]);
    state.meta = meta;
    state.store = normalizeStore(loadedStore);

    ensureSelection();
    render();

    try {
      document.execCommand("styleWithCSS", false, false);
    } catch (_error) {
      // Ignore unsupported styleWithCSS calls.
    }

    state.unsubscribeCommand = window.notesAPI.onCommand(handleAppCommand);
  }

  function teardown() {
    if (typeof state.unsubscribeCommand === "function") {
      state.unsubscribeCommand();
    }
  }

  function cacheDom() {
    dom.folderNav = document.getElementById("folder-nav");
    dom.newFolderButton = document.getElementById("new-folder-button");
    dom.newNoteButton = document.getElementById("new-note-button");
    dom.searchInput = document.getElementById("search-input");
    dom.listContext = document.getElementById("list-context");
    dom.listSubtitle = document.getElementById("list-subtitle");
    dom.noteCount = document.getElementById("note-count");
    dom.noteList = document.getElementById("note-list");
    dom.emptyState = document.getElementById("empty-state");
    dom.editorSection = document.getElementById("editor-section");
    dom.emptyNewNoteButton = document.getElementById("empty-new-note-button");
    dom.emptyNewFolderButton = document.getElementById("empty-new-folder-button");
    dom.noteTitleInput = document.getElementById("note-title-input");
    dom.pinNoteButton = document.getElementById("pin-note-button");
    dom.deleteNoteButton = document.getElementById("delete-note-button");
    dom.noteMeta = document.getElementById("note-meta");
    dom.folderSelect = document.getElementById("folder-select");
    dom.editor = document.getElementById("editor");
    dom.formatToolbar = document.querySelector(".format-toolbar");
  }

  function bindEvents() {
    dom.newFolderButton.addEventListener("click", createFolderFromPrompt);
    dom.newNoteButton.addEventListener("click", () => createNote({ focusTarget: "title" }));
    dom.emptyNewNoteButton.addEventListener("click", () => createNote({ focusTarget: "title" }));
    dom.emptyNewFolderButton.addEventListener("click", createFolderFromPrompt);

    dom.searchInput.addEventListener("input", () => {
      state.searchQuery = dom.searchInput.value.trim();
      ensureSelection();
      render();
    });

    dom.folderNav.addEventListener("click", (event) => {
      const deleteButton = event.target.closest("[data-folder-delete]");
      if (deleteButton) {
        const folderId = deleteButton.getAttribute("data-folder-delete");
        deleteFolder(folderId);
        return;
      }

      const button = event.target.closest("[data-sidebar-id]");
      if (!button) {
        return;
      }

      state.store.preferences.selectedSidebarId = button.getAttribute("data-sidebar-id");
      ensureSelection();
      render();
      persistStore();
    });

    dom.noteList.addEventListener("click", (event) => {
      const button = event.target.closest("[data-note-id]");
      if (!button) {
        return;
      }

      state.store.preferences.selectedNoteId = button.getAttribute("data-note-id");
      render();
      persistStore();
    });

    dom.noteTitleInput.addEventListener("input", () => {
      const note = getActiveNote();
      if (!note) {
        return;
      }

      note.title = dom.noteTitleInput.value;
      note.updatedAt = new Date().toISOString();
      render({ syncEditor: false });
      persistStore();
    });

    dom.pinNoteButton.addEventListener("click", togglePinOnActiveNote);
    dom.deleteNoteButton.addEventListener("click", deleteActiveNote);

    dom.folderSelect.addEventListener("change", () => {
      const note = getActiveNote();
      if (!note) {
        return;
      }

      note.folderId = dom.folderSelect.value;
      note.updatedAt = new Date().toISOString();
      ensureSelection();
      render();
      persistStore();
    });

    dom.formatToolbar.addEventListener("click", (event) => {
      const button = event.target.closest("[data-format]");
      if (!button) {
        return;
      }

      applyFormatting(button.getAttribute("data-format"));
    });

    dom.editor.addEventListener("input", () => {
      syncActiveNoteFromEditor({ sanitize: false });
      render({ syncEditor: false });
      persistStore();
    });

    dom.editor.addEventListener("blur", () => {
      syncActiveNoteFromEditor({ sanitize: true, pushToEditor: true });
      render({ syncEditor: true });
      persistStore();
    });

    dom.editor.addEventListener("keydown", (event) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
        event.preventDefault();
        syncActiveNoteFromEditor({ sanitize: true, pushToEditor: true });
        persistStore();
      }
    });

    document.addEventListener("selectionchange", () => {
      if (document.activeElement === dom.editor) {
        updateFormattingStates();
      }
    });
  }

  function normalizeStore(rawStore) {
    const initialFolder = createFolderRecord("备忘录", true);
    const folders = Array.isArray(rawStore?.folders) ? rawStore.folders : [initialFolder];
    const normalizedFolders = folders
      .filter((folder) => folder && typeof folder.id === "string")
      .map((folder) => ({
        id: folder.id,
        name: typeof folder.name === "string" && folder.name.trim() ? folder.name.trim() : "未命名文件夹",
        createdAt: normalizeDate(folder.createdAt),
        isDefault: Boolean(folder.isDefault)
      }));

    if (normalizedFolders.length === 0) {
      normalizedFolders.push(initialFolder);
    }

    const folderIds = new Set(normalizedFolders.map((folder) => folder.id));

    const normalizedNotes = (Array.isArray(rawStore?.notes) ? rawStore.notes : [])
      .filter((note) => note && typeof note.id === "string")
      .map((note) => {
        const bodyHtml = typeof note.bodyHtml === "string" ? note.bodyHtml : "";
        const createdAt = normalizeDate(note.createdAt);
        const updatedAt = normalizeDate(note.updatedAt, createdAt);

        return {
          id: note.id,
          folderId: folderIds.has(note.folderId) ? note.folderId : normalizedFolders[0].id,
          title: typeof note.title === "string" ? note.title : "",
          bodyHtml,
          bodyText: typeof note.bodyText === "string" ? note.bodyText : stripHtml(bodyHtml),
          pinned: Boolean(note.pinned),
          createdAt,
          updatedAt
        };
      });

    return {
      version: Number(rawStore?.version) || 1,
      preferences: {
        selectedSidebarId:
          typeof rawStore?.preferences?.selectedSidebarId === "string"
            ? rawStore.preferences.selectedSidebarId
            : "all-notes",
        selectedNoteId:
          typeof rawStore?.preferences?.selectedNoteId === "string"
            ? rawStore.preferences.selectedNoteId
            : normalizedNotes[0]?.id || null
      },
      folders: normalizedFolders,
      notes: normalizedNotes
    };
  }

  function normalizeDate(value, fallback = new Date().toISOString()) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return fallback;
    }

    return date.toISOString();
  }

  function createFolderRecord(name, isDefault) {
    return {
      id: `folder-${crypto.randomUUID()}`,
      name,
      createdAt: new Date().toISOString(),
      isDefault: Boolean(isDefault)
    };
  }

  function createNoteRecord(folderId) {
    const createdAt = new Date().toISOString();

    return {
      id: `note-${crypto.randomUUID()}`,
      folderId,
      title: "",
      bodyHtml: "",
      bodyText: "",
      pinned: false,
      createdAt,
      updatedAt: createdAt
    };
  }

  function ensureSelection() {
    if (!state.store.folders.length) {
      state.store.folders.push(createFolderRecord("备忘录", true));
    }

    const validSidebarIds = new Set(["all-notes", "pinned-notes", ...state.store.folders.map((folder) => folder.id)]);
    if (!validSidebarIds.has(state.store.preferences.selectedSidebarId)) {
      state.store.preferences.selectedSidebarId = "all-notes";
    }

    const visibleNotes = getVisibleNotes();
    if (!visibleNotes.some((note) => note.id === state.store.preferences.selectedNoteId)) {
      state.store.preferences.selectedNoteId = visibleNotes[0]?.id || null;
    }
  }

  function getVisibleNotes() {
    let notes = [...state.store.notes];
    const sidebarId = state.store.preferences.selectedSidebarId;
    const query = state.searchQuery.toLowerCase();

    if (sidebarId === "pinned-notes") {
      notes = notes.filter((note) => note.pinned);
    } else if (sidebarId !== "all-notes") {
      notes = notes.filter((note) => note.folderId === sidebarId);
    }

    if (query) {
      notes = notes.filter((note) => {
        const haystack = `${getDisplayTitle(note)} ${note.bodyText || ""}`.toLowerCase();
        return haystack.includes(query);
      });
    }

    notes.sort((left, right) => {
      if (left.pinned !== right.pinned) {
        return left.pinned ? -1 : 1;
      }

      return new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
    });

    return notes;
  }

  function getDisplayTitle(note) {
    if (note.title && note.title.trim()) {
      return note.title.trim();
    }

    const preview = (note.bodyText || "").trim();
    return preview ? preview.slice(0, 30) : "无标题";
  }

  function getActiveNote() {
    return state.store.notes.find((note) => note.id === state.store.preferences.selectedNoteId) || null;
  }

  function getFolderById(folderId) {
    return state.store.folders.find((folder) => folder.id === folderId) || null;
  }

  function render(options = {}) {
    const syncEditor = options.syncEditor !== false;

    renderFolderNav();
    renderNoteList();
    renderEditor(syncEditor);
  }

  function renderFolderNav() {
    const counts = new Map();
    counts.set("all-notes", state.store.notes.length);
    counts.set("pinned-notes", state.store.notes.filter((note) => note.pinned).length);

    state.store.folders.forEach((folder) => {
      counts.set(folder.id, state.store.notes.filter((note) => note.folderId === folder.id).length);
    });

    const smartMarkup = SMART_ITEMS.map((item) =>
      renderFolderButton({
        id: item.id,
        name: item.name,
        hint: item.hint,
        count: counts.get(item.id) || 0,
        active: state.store.preferences.selectedSidebarId === item.id,
        deletable: false
      })
    ).join("");

    const folderMarkup = state.store.folders
      .map((folder) =>
        renderFolderButton({
          id: folder.id,
          name: folder.name,
          hint: folder.isDefault ? "默认文件夹" : "自定义文件夹",
          count: counts.get(folder.id) || 0,
          active: state.store.preferences.selectedSidebarId === folder.id,
          deletable: !folder.isDefault
        })
      )
      .join("");

    dom.folderNav.innerHTML = `
      <p class="folder-section-label">智能文件夹</p>
      ${smartMarkup}
      <p class="folder-section-label">我的文件夹</p>
      ${folderMarkup}
    `;
  }

  function renderFolderButton(folder) {
    return `
      <button class="folder-item ${folder.active ? "active" : ""}" data-sidebar-id="${escapeHtml(folder.id)}" type="button">
        <span class="folder-name-wrap">
          <span class="folder-name">${escapeHtml(folder.name)}</span>
          <span class="folder-note">${escapeHtml(folder.hint)}</span>
        </span>
        <span class="folder-badge">${folder.count}</span>
        ${
          folder.deletable
            ? `<span class="folder-delete" data-folder-delete="${escapeHtml(folder.id)}" aria-label="删除文件夹">×</span>`
            : "<span></span>"
        }
      </button>
    `;
  }

  function renderNoteList() {
    const sidebarId = state.store.preferences.selectedSidebarId;
    const visibleNotes = getVisibleNotes();
    const activeFolder = getFolderById(sidebarId);

    dom.listContext.textContent =
      sidebarId === "all-notes" ? "全部备忘录" : sidebarId === "pinned-notes" ? "已置顶" : activeFolder?.name || "全部备忘录";

    dom.listSubtitle.textContent = state.searchQuery
      ? `匹配 “${state.searchQuery}” 的结果`
      : "按最近编辑时间排序，置顶优先";
    dom.noteCount.textContent = String(visibleNotes.length);

    if (visibleNotes.length === 0) {
      dom.noteList.innerHTML = `
        <div class="list-empty">
          <p>当前没有可显示的备忘录。</p>
          <p>试试新建内容，或者切换文件夹。</p>
        </div>
      `;
      return;
    }

    dom.noteList.innerHTML = visibleNotes
      .map((note) => {
        const folder = getFolderById(note.folderId);
        return `
          <button class="note-card ${note.id === state.store.preferences.selectedNoteId ? "active" : ""}" data-note-id="${escapeHtml(note.id)}" type="button">
            <div class="note-card-header">
              <p class="note-card-title">${escapeHtml(getDisplayTitle(note))}</p>
              ${note.pinned ? '<span class="pin-mark">已置顶</span>' : "<span></span>"}
            </div>
            <p class="note-card-preview">${escapeHtml(note.bodyText || "开始记录你的第一段内容...")}</p>
            <div class="note-card-footer">
              <span class="note-card-meta">${formatDate(note.updatedAt)}</span>
              <span class="note-card-folder">${escapeHtml(folder?.name || "未分类")}</span>
            </div>
          </button>
        `;
      })
      .join("");
  }

  function renderEditor(syncEditor) {
    const note = getActiveNote();

    if (!note) {
      dom.emptyState.classList.remove("hidden");
      dom.editorSection.classList.add("hidden");
      document.title = "WinNotes";
      return;
    }

    dom.emptyState.classList.add("hidden");
    dom.editorSection.classList.remove("hidden");

    if (syncEditor) {
      dom.noteTitleInput.value = note.title;
      dom.editor.innerHTML = note.bodyHtml;
    }

    renderFolderSelect(note.folderId);

    const folder = getFolderById(note.folderId);
    dom.noteMeta.textContent = `${folder?.name || "未分类"} · 最近编辑 ${formatDateTime(note.updatedAt)}`;
    dom.pinNoteButton.textContent = note.pinned ? "取消置顶" : "置顶";
    dom.pinNoteButton.classList.toggle("active", note.pinned);
    document.title = `${getDisplayTitle(note)} · WinNotes`;

    updateFormattingStates();
  }

  function renderFolderSelect(selectedFolderId) {
    dom.folderSelect.innerHTML = state.store.folders
      .map(
        (folder) =>
          `<option value="${escapeHtml(folder.id)}" ${folder.id === selectedFolderId ? "selected" : ""}>${escapeHtml(folder.name)}</option>`
      )
      .join("");
  }

  function createFolderFromPrompt() {
    const name = window.prompt("输入文件夹名称");
    if (!name || !name.trim()) {
      return;
    }

    const folder = createFolderRecord(name.trim(), false);
    state.store.folders.push(folder);
    state.store.preferences.selectedSidebarId = folder.id;
    ensureSelection();
    render();
    persistStore();
  }

  function resolveFolderForNewNote() {
    const sidebarId = state.store.preferences.selectedSidebarId;

    if (state.store.folders.some((folder) => folder.id === sidebarId)) {
      return sidebarId;
    }

    const activeNote = getActiveNote();
    if (activeNote) {
      return activeNote.folderId;
    }

    return state.store.folders[0].id;
  }

  function createNote(options = {}) {
    const note = createNoteRecord(resolveFolderForNewNote());
    state.store.notes.push(note);
    state.store.preferences.selectedNoteId = note.id;
    ensureSelection();
    render();
    persistStore();

    queueMicrotask(() => {
      if (options.focusTarget === "title") {
        dom.noteTitleInput.focus();
      } else {
        dom.editor.focus();
      }
    });
  }

  function togglePinOnActiveNote() {
    const note = getActiveNote();
    if (!note) {
      return;
    }

    note.pinned = !note.pinned;
    note.updatedAt = new Date().toISOString();
    ensureSelection();
    render();
    persistStore();
  }

  function deleteFolder(folderId) {
    const folder = getFolderById(folderId);
    if (!folder) {
      return;
    }

    const targetFolder = state.store.folders.find((item) => item.id !== folderId);
    if (!targetFolder) {
      window.alert("至少需要保留一个文件夹。");
      return;
    }

    const shouldDelete = window.confirm(`删除 “${folder.name}” 后，里面的备忘录会移动到 “${targetFolder.name}”。是否继续？`);
    if (!shouldDelete) {
      return;
    }

    state.store.notes.forEach((note) => {
      if (note.folderId === folderId) {
        note.folderId = targetFolder.id;
        note.updatedAt = new Date().toISOString();
      }
    });

    state.store.folders = state.store.folders.filter((item) => item.id !== folderId);
    if (state.store.preferences.selectedSidebarId === folderId) {
      state.store.preferences.selectedSidebarId = targetFolder.id;
    }

    ensureSelection();
    render();
    persistStore();
  }

  function deleteActiveNote() {
    const note = getActiveNote();
    if (!note) {
      return;
    }

    const shouldDelete = window.confirm(`删除 “${getDisplayTitle(note)}” ？`);
    if (!shouldDelete) {
      return;
    }

    state.store.notes = state.store.notes.filter((item) => item.id !== note.id);
    ensureSelection();
    render();
    persistStore();
  }

  function syncActiveNoteFromEditor(options = {}) {
    const note = getActiveNote();
    if (!note) {
      return;
    }

    let bodyHtml = dom.editor.innerHTML;
    if (options.sanitize) {
      bodyHtml = sanitizeNoteHtml(bodyHtml);
    }

    note.bodyHtml = bodyHtml;
    note.bodyText = stripHtml(bodyHtml);
    note.updatedAt = new Date().toISOString();

    if (options.pushToEditor) {
      dom.editor.innerHTML = bodyHtml;
    }
  }

  function applyFormatting(type) {
    const note = getActiveNote();
    if (!note) {
      return;
    }

    dom.editor.focus();

    switch (type) {
      case "bold":
        document.execCommand("bold");
        break;
      case "italic":
        document.execCommand("italic");
        break;
      case "underline":
        document.execCommand("underline");
        break;
      case "heading":
        document.execCommand("formatBlock", false, "<h2>");
        break;
      case "unordered":
        document.execCommand("insertUnorderedList");
        break;
      case "ordered":
        document.execCommand("insertOrderedList");
        break;
      case "quote":
        document.execCommand("formatBlock", false, "<blockquote>");
        break;
      case "clear":
        document.execCommand("removeFormat");
        break;
      default:
        return;
    }

    syncActiveNoteFromEditor({ sanitize: true, pushToEditor: true });
    render({ syncEditor: false });
    persistStore();
  }

  function updateFormattingStates() {
    document.querySelectorAll("[data-format]").forEach((button) => {
      const format = button.getAttribute("data-format");
      let isActive = false;

      try {
        if (format === "bold") {
          isActive = document.queryCommandState("bold");
        } else if (format === "italic") {
          isActive = document.queryCommandState("italic");
        } else if (format === "underline") {
          isActive = document.queryCommandState("underline");
        }
      } catch (_error) {
        isActive = false;
      }

      button.classList.toggle("active", isActive);
    });
  }

  function sanitizeNoteHtml(rawHtml) {
    const template = document.createElement("template");
    template.innerHTML = rawHtml;

    const allowedTags = new Set([
      "A",
      "B",
      "BLOCKQUOTE",
      "BR",
      "DIV",
      "EM",
      "H1",
      "H2",
      "H3",
      "H4",
      "H5",
      "H6",
      "I",
      "LI",
      "OL",
      "P",
      "STRONG",
      "U",
      "UL"
    ]);

    const walk = (node) => {
      [...node.childNodes].forEach((child) => {
        if (child.nodeType === Node.COMMENT_NODE) {
          child.remove();
          return;
        }

        if (child.nodeType !== Node.ELEMENT_NODE) {
          return;
        }

        if (!allowedTags.has(child.tagName)) {
          const parentNode = child.parentNode;
          child.replaceWith(...child.childNodes);
          if (parentNode) {
            walk(parentNode);
          }
          return;
        }

        [...child.attributes].forEach((attribute) => {
          const name = attribute.name.toLowerCase();
          if (child.tagName === "A" && name === "href") {
            if (!/^https?:/i.test(attribute.value)) {
              child.removeAttribute("href");
            }
          } else {
            child.removeAttribute(attribute.name);
          }
        });

        if (child.tagName === "A" && child.getAttribute("href")) {
          child.setAttribute("target", "_blank");
          child.setAttribute("rel", "noreferrer noopener");
        }

        walk(child);
      });
    };

    walk(template.content);
    return template.innerHTML.trim();
  }

  function handleAppCommand(command) {
    switch (command) {
      case "new-note":
        createNote({ focusTarget: "title" });
        break;
      case "new-folder":
        createFolderFromPrompt();
        break;
      case "focus-search":
        dom.searchInput.focus();
        dom.searchInput.select();
        break;
      case "delete-note":
        deleteActiveNote();
        break;
      case "toggle-pin":
        togglePinOnActiveNote();
        break;
      default:
        break;
    }
  }

  function debounce(fn, wait) {
    let timeoutId = null;

    return (...args) => {
      window.clearTimeout(timeoutId);
      timeoutId = window.setTimeout(() => fn(...args), wait);
    };
  }

  function stripHtml(html) {
    const container = document.createElement("div");
    container.innerHTML = html || "";
    return (container.textContent || "").replace(/\s+/g, " ").trim();
  }

  function formatDate(value) {
    return new Intl.DateTimeFormat("zh-CN", {
      month: "short",
      day: "numeric"
    }).format(new Date(value));
  }

  function formatDateTime(value) {
    return new Intl.DateTimeFormat("zh-CN", {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit"
    }).format(new Date(value));
  }

  function escapeHtml(text) {
    return String(text)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }
})();
