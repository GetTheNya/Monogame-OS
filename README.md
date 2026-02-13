<div align="center">
  <img src="Icon.png" alt="HentOS Logo" width="128" height="128" />

  # HentOS

  ![Language](https://img.shields.io/badge/Language-CSharp-239120)
  ![Framework](https://img.shields.io/badge/Framework-Monogame-orange)
  ![Platform](https://img.shields.io/badge/Platform-Windows-blue)

  <p>
    <b>A sophisticated Operating System Simulator built in C# with Monogame.</b>
  </p>
</div>

**HentOS** is a sophisticated **Operating System Simulator** built entirely in **C#** using the **Monogame** framework. It features a fully functional simulated kernel, a windowing system, a virtual file system, and a suite of capable applications.

Designed to mimic the feel of a modern desktop environment, HentOS provides a platform for running virtual applications (`.sapp`), managing files, and even browsing a simulated network.

---

## ✨ Features

### 🧠 Core System
*   **Virtual File System (VFS)**: A complete hierarchical file system simulation with drive mapping (`C:\`), file streams, and standard IO operations.
*   **Registry**: A Windows-like registry system (`HKCU`, `HKLM`) for system configuration and state persistence.
*   **Process Manager**: Preemptive multitasking simulation with process isolation, priority scheduling, and lifecycle management.

### 🎨 Desktop & Shell
*   **Window Manager**: Draggable, resizable windows with minimizing, maximizing, and snapping capabilities.
*   **Taskbar & Start Menu**: Fully functional taskbar with running tasks, system tray, and a classic start menu.
*   **Notifications**: Toast notification system for system events and app alerts.

### 🌐 Networking
*   **Network Stack**: Simulated network layer capable of resolving virtual addresses.
*   **HentHub Store**: A built-in "App Store" to browse, download, and install new applications and games.
*   **Updates**: Integrated system for keeping applications up to date.

### 🛠️ Developer Tools
*   **Custom Compiler**: Built-in compiler for `.sapp` (Simulated Application) files.
*   **Hot Reload**: Change code and see updates instantly without restarting the simulator.
*   **Terminal**: A Unix-like terminal environment with support for piping (`|`), redirection (`>`), and standard utilities.

---

## 🚀 Included Applications

HentOS comes pre-loaded with a suite of essential software:

| Application | Description |
| :--- | :--- |
| **Explorer** | Navigate the Virtual File System, manage files and folders. |
| **Terminal** | Command-line interface with tools like `grep`, `cat`, `ls`, `mv`, `rm`. |
| **Notepad** | Simple text editor for viewing and editing files. |
| **Settings** | Configure OS appearance, user details, and system preferences. |
| **Process Manager** | View and kill running processes and monitor system resources. |
| **Image Viewer** | View images stored in the VFS. |
| **HentHub Store** | Download new apps and games. |

---

## 🛠️ Getting Started

### Prerequisites
*   [**C# / .NET 8.0 SDK**](https://dotnet.microsoft.com/download)
*   [**Visual Studio 2022**](https://visualstudio.microsoft.com/) (Recommended)

### Building and Running
1.  **Clone the repository**:
    ```bash
    git clone https://github.com/GetTheNya/Monogame-OS.git
    cd Monogame-OS
    ```

2.  **Restore dependencies**:
    ```bash
    dotnet restore
    ```

3.  **Run the Simulator**:
    ```bash
    dotnet run
    ```

---

## 🤝 Contributing

Contributions are welcome! Whether you want to fix a bug, add a new terminal command, or build a whole new `.sapp` application.

1.  Fork the Project
2.  Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3.  Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4.  Push to the Branch (`git push origin feature/AmazingFeature`)
5.  Open a Pull Request

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

Third-party software used in this project is listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
