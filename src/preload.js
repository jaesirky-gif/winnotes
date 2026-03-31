const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("notesAPI", {
  loadStore: () => ipcRenderer.invoke("notes:load"),
  saveStore: (store) => ipcRenderer.invoke("notes:save", store),
  getMeta: () => ipcRenderer.invoke("app:get-meta"),
  onCommand: (handler) => {
    const wrapped = (_event, command) => handler(command);
    ipcRenderer.on("app:command", wrapped);

    return () => {
      ipcRenderer.removeListener("app:command", wrapped);
    };
  }
});
