const { app, BrowserWindow, Menu, ipcMain, shell } = require("electron");
const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const { existsSync } = require("node:fs");
const path = require("node:path");

const DB_VERSION = 1;
const SMART_SIDEBAR_IDS = new Set(["all-notes", "pinned-notes"]);

let mainWindow = null;

function createId(prefix) {
  return `${prefix}-${crypto.randomUUID()}`;
}

function isoNow() {
  return new Date().toISOString();
}

function stripHtml(html) {
  return String(html || "")
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function normalizeIsoString(value, fallback) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  return date.toISOString();
}

function createDefaultStore() {
  const notesFolderId = createId("folder");
  const workFolderId = createId("folder");
  const ideasFolderId = createId("folder");

  const kickoffNoteId = createId("note");
  const designNoteId = createId("note");
  const tripsNoteId = createId("note");

  const createdAt = isoNow();

  return {
    version: DB_VERSION,
    preferences: {
      selectedSidebarId: "all-notes",
      selectedNoteId: kickoffNoteId
    },
    folders: [
      {
        id: notesFolderId,
        name: "备忘录",
        createdAt,
        isDefault: true
      },
      {
        id: workFolderId,
        name: "工作",
        createdAt,
        isDefault: true
      },
      {
        id: ideasFolderId,
        name: "灵感",
        createdAt,
        isDefault: true
      }
    ],
    notes: [
      {
        id: kickoffNoteId,
        folderId: notesFolderId,
        title: "欢迎使用 WinNotes",
        bodyHtml:
          "<p>用接近 macOS Notes 的三栏结构来记录日常想法。</p><p><strong>已支持：</strong> 文件夹、搜索、置顶、富文本编辑、自动保存。</p>",
        bodyText: "用接近 macOS Notes 的三栏结构来记录日常想法。已支持： 文件夹、搜索、置顶、富文本编辑、自动保存。",
        pinned: true,
        createdAt,
        updatedAt: createdAt
      },
      {
        id: designNoteId,
        folderId: workFolderId,
        title: "产品评审清单",
        bodyHtml:
          "<h2>本周重点</h2><ul><li>整理发布节奏</li><li>补齐首页转化数据</li><li>确认视觉稿交付时间</li></ul>",
        bodyText: "本周重点 整理发布节奏 补齐首页转化数据 确认视觉稿交付时间",
        pinned: false,
        createdAt,
        updatedAt: createdAt
      },
      {
        id: tripsNoteId,
        folderId: ideasFolderId,
        title: "周末随记",
        bodyHtml:
          "<p>把好点子先写下来，再决定哪些值得继续做。</p><blockquote>先把东西做出来，再打磨细节。</blockquote>",
        bodyText: "把好点子先写下来，再决定哪些值得继续做。先把东西做出来，再打磨细节。",
        pinned: false,
        createdAt,
        updatedAt: createdAt
      }
    ]
  };
}

function normalizeStore(input) {
  const fallback = createDefaultStore();

  const folders = Array.isArray(input?.folders)
    ? input.folders
        .filter((folder) => folder && typeof folder.id === "string")
        .map((folder) => ({
          id: folder.id,
          name:
            typeof folder.name === "string" && folder.name.trim()
              ? folder.name.trim()
              : "未命名文件夹",
          createdAt: normalizeIsoString(folder.createdAt, isoNow()),
          isDefault: Boolean(folder.isDefault)
        }))
    : fallback.folders;

  if (folders.length === 0) {
    folders.push(...fallback.folders);
  }

  const folderIds = new Set(folders.map((folder) => folder.id));
  const fallbackFolderId = folders[0].id;

  const notes = Array.isArray(input?.notes)
    ? input.notes
        .filter((note) => note && typeof note.id === "string")
        .map((note) => {
          const createdAt = normalizeIsoString(note.createdAt, isoNow());
          const updatedAt = normalizeIsoString(note.updatedAt, createdAt);

          return {
            id: note.id,
            folderId: folderIds.has(note.folderId) ? note.folderId : fallbackFolderId,
            title: typeof note.title === "string" ? note.title : "",
            bodyHtml: typeof note.bodyHtml === "string" ? note.bodyHtml : "",
            bodyText:
              typeof note.bodyText === "string"
                ? note.bodyText
                : stripHtml(note.bodyHtml),
            pinned: Boolean(note.pinned),
            createdAt,
            updatedAt
          };
        })
    : fallback.notes;

  const selectedSidebarCandidate = input?.preferences?.selectedSidebarId;
  const selectedSidebarId =
    SMART_SIDEBAR_IDS.has(selectedSidebarCandidate) || folderIds.has(selectedSidebarCandidate)
      ? selectedSidebarCandidate
      : "all-notes";

  const selectedNoteCandidate = input?.preferences?.selectedNoteId;
  const selectedNoteId = notes.some((note) => note.id === selectedNoteCandidate)
    ? selectedNoteCandidate
    : notes[0]?.id || null;

  return {
    version: DB_VERSION,
    preferences: {
      selectedSidebarId,
      selectedNoteId
    },
    folders,
    notes
  };
}

function getStoreFilePath() {
  return path.join(app.getPath("userData"), "winnotes-data.json");
}

async function loadStore() {
  const storePath = getStoreFilePath();

  if (!existsSync(storePath)) {
    return createDefaultStore();
  }

  try {
    const contents = await fs.readFile(storePath, "utf8");
    const parsed = JSON.parse(contents);
    return normalizeStore(parsed);
  } catch (error) {
    console.error("Failed to load store, using defaults.", error);
    return createDefaultStore();
  }
}

async function saveStore(nextStore) {
  const storePath = getStoreFilePath();
  const normalized = normalizeStore(nextStore);

  await fs.mkdir(path.dirname(storePath), { recursive: true });
  await fs.writeFile(storePath, JSON.stringify(normalized, null, 2), "utf8");

  return normalized;
}

function sendCommand(command) {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return;
  }

  mainWindow.webContents.send("app:command", command);
}

function createApplicationMenu() {
  const isMac = process.platform === "darwin";

  const template = [
    ...(isMac
      ? [
          {
            label: app.name,
            submenu: [
              { role: "about" },
              { type: "separator" },
              { role: "services" },
              { type: "separator" },
              { role: "hide" },
              { role: "hideOthers" },
              { role: "unhide" },
              { type: "separator" },
              { role: "quit" }
            ]
          }
        ]
      : []),
    {
      label: "文件",
      submenu: [
        {
          label: "新建备忘录",
          accelerator: "CmdOrCtrl+N",
          click: () => sendCommand("new-note")
        },
        {
          label: "新建文件夹",
          accelerator: "CmdOrCtrl+Shift+N",
          click: () => sendCommand("new-folder")
        },
        { type: "separator" },
        {
          label: "置顶或取消置顶",
          accelerator: "CmdOrCtrl+P",
          click: () => sendCommand("toggle-pin")
        },
        {
          label: "删除当前备忘录",
          accelerator: isMac ? "Backspace" : "Delete",
          click: () => sendCommand("delete-note")
        },
        { type: "separator" },
        ...(isMac ? [] : [{ role: "quit", label: "退出" }])
      ]
    },
    {
      label: "编辑",
      submenu: [
        { role: "undo", label: "撤销" },
        { role: "redo", label: "重做" },
        { type: "separator" },
        { role: "cut", label: "剪切" },
        { role: "copy", label: "复制" },
        { role: "paste", label: "粘贴" },
        { role: "pasteAndMatchStyle", label: "粘贴并匹配样式" },
        { role: "delete", label: "删除" },
        { role: "selectAll", label: "全选" },
        { type: "separator" },
        {
          label: "搜索",
          accelerator: "CmdOrCtrl+F",
          click: () => sendCommand("focus-search")
        }
      ]
    },
    {
      label: "视图",
      submenu: [
        { role: "reload", label: "重新加载" },
        { role: "forceReload", label: "强制重新加载" },
        { role: "toggleDevTools", label: "开发者工具" },
        { type: "separator" },
        { role: "resetZoom", label: "实际大小" },
        { role: "zoomIn", label: "放大" },
        { role: "zoomOut", label: "缩小" },
        { type: "separator" },
        { role: "togglefullscreen", label: "全屏" }
      ]
    }
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1460,
    height: 940,
    minWidth: 1120,
    minHeight: 720,
    backgroundColor: "#efe4cf",
    title: "WinNotes",
    titleBarStyle: process.platform === "darwin" ? "hiddenInset" : "default",
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  mainWindow.loadFile(path.join(__dirname, "renderer", "index.html"));

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    void shell.openExternal(url);
    return { action: "deny" };
  });

  mainWindow.webContents.on("will-navigate", (event, url) => {
    const currentUrl = mainWindow?.webContents.getURL();
    if (currentUrl && url !== currentUrl) {
      event.preventDefault();
      void shell.openExternal(url);
    }
  });
}

ipcMain.handle("notes:load", async () => loadStore());
ipcMain.handle("notes:save", async (_event, nextStore) => saveStore(nextStore));
ipcMain.handle("app:get-meta", async () => ({
  platform: process.platform,
  version: app.getVersion()
}));

app.whenReady().then(() => {
  app.setName("WinNotes");
  createApplicationMenu();
  createWindow();

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
