## DirtyDiana: A BadBuilder Linux port
DirtyDiana is a tool for creating a BadUpdate USB drive for the Xbox 360.

## License
This project is licensed under the BSD-3-Clause License.

This repository is a fork of [BadBuilder](https://github.com/Pdawg-bytes/BadBuilder).
All original rights belong to the original author(s).
Modifications in this fork are [©Made2Flex](https://github.com/Made2Flex/DirtyDiana).

## Features:
### USB Formatting
- Uses a custom FAT32 formatter that supports large USB drives (≥32GB).
- Ensures compatibility with the Xbox 360.

> [!NOTE]
⚠ On Linux, DirtyDiana needs to be run with sudo!

### Automatic File Downloading
- Detects and downloads the latest required files automatically.
- Recognizes previously downloaded files and reuses them by default.
- Allows specifying custom paths for required files if they are already on your system.
> [!IMPORTANT]
> DirtyDiana does not dynamically locate files inside ZIP archives. If your provided archive has a different folder structure than expected, the process will fail abruptly. Ensure your archive matches the expected format if specifying an existing copy.

### File Extraction & Copying
- Extracts all necessary files automatically.
- Prepares the USB drive for the BadUpdate exploit by copying all required files.

### Homebrew Support
- Allows adding homebrew applications by specifying their root folder.
- Automatically searches for the entry point (`.xex`) file within the folder.
- If multiple `.xex` files are found, DirtyDiana will prompt you to select the correct one.
- Copies all necessary files and patches the entry `.xex` using the downloaded XexTool. 
> [!NOTE]
> As of the latest releases, patching is no longer needed. A legacy toggle was left in src code for you to use if ever need be.

## How to Use
1. **Launch the executable**. It will open inside of a Terminal window.
> [!IMPORTANT] 
> On Linux, you MUST manually run DirtyDiana inside a terminal. Because the use of 'sudo' is required. This behavior will change in future releases.
2. **Formatting:** DirtyDiana will format your USB drive as FAT32, even if it’s larger than 32GB.
> [!CAUTION]
> Formatting a disk means that all data will be lost. Make sure you have selected the right drive before confirming the format. I am not responsible for any data loss.
3. **Download Files:** DirtyDiana will fetch the required exploit files or let you specify an existing location.
4. **Extract Files:** DirtyDiana will automatically extract everything needed.
5. **Select default program**: DirtyDiana will prompt you to choose a program that BadUpdate will try and invoke, being either [FreeMyXe](https://github.com/FreeMyXe/FreeMyXe), or [XeUnshackle](https://github.com/Byrom90/XeUnshackle)
6. **Select default exploit**: DirtyDiana will prompt you to choose between [BadUpdate](https://github.com/grimdoomer/Xbox360BadUpdate) or [ABadAvatar](https://github.com/shutterbug2000/ABadAvatar)
7. **Copy Files:** DirtyDiana will copy all of the extracted files to the correct locations.
8. **Add Homebrew (Optional):**
    - Specify the root folder of your homebrew application (e.g., `D:\Aurora 0.7b.2 - Release Package`).
    - If no `.xex` files were located in the root folder, DirtyDiana will prompt you for the path of the entry point.
    - DirtyDiana will locate the `.xex` file inside.
    - If multiple `.xex` files exist, you’ll be prompted to choose the correct entry point.
    - First, all necessary files will be copied, then, the `.xex` file will be patched using **XexTool** (Optional). 
    - This ensures that the original copy of the homebrew program will **not** be modified, as it is instead done in-place on the USB drive.

## Example Homebrew Folder Structure
If you want to add Aurora, you would select the **root folder**, like:

```
D:\Aurora 0.7b.2 - Release Package
```

Which contains:

```
Aurora 0.7b.2 - Release Package/
├── Data/
├── Media/
├── Plugins/
├── Skins/
├── User/
├── Aurora.xex
├── live.json
├── nxeart
```
DirtyDiana will detect `Aurora.xex` as the entry point and patch it accordingly.

> [!IMPORTANT]
> Homebrew apps which do not contain the entry point in the root folder will require you to manually enter the path of the entry point.

## Building instructions for linux
1. Clone the repo: https://github.com/Made2Flex/DirtyDiana
2. `cd DirtyDiana`
3. Run the build script from the project root:
   `bash ./linux_build.sh`
4. The final executable will be placed in: `~/Desktop/DirtyDiana`

### Manual build (without script)
1. Install dotnet-sdk (`dotnet-sdk-8.0` on Debian/Ubuntu)
2. Restore dependencies:
   `dotnet restore DirtyDiana/DirtyDiana.csproj`
3. Build Release mode:
   `dotnet build DirtyDiana/DirtyDiana.csproj -c Release --no-restore`
4. Publish for Linux x64:
   `dotnet publish DirtyDiana/DirtyDiana.csproj -c Release -r linux-x64 -p:PublishSingleFile=false -p:PublishAot=true --self-contained true -p:DebugType=none -p:WarningLevel=0 -p:Optimize=true`
5. Published binary will be in:
   `DirtyDiana/bin/Release/net8.0/linux-x64/publish/`

## Building instructions for M$ Windows
1. Open a PowerShell window as admin
2. Cd into the root directory of the project
3. Run this cmd:
   `powershell -ExecutionPolicy Bypass -File .\windows_build.ps1`
5. The final executable will be placed in: `Desktop/DirtyDiana/`

## Reporting Issues
If you encounter any problems, please create a new issue with details about your setup and the problem.

### Credits
- **Grimdoomer:** [BadUpdate](https://github.com/grimdoomer/Xbox360BadUpdate)
- **InvoxiPlayGames:** [FreeMyXe](https://github.com/FreeMyXe/FreeMyXe)
- **Byrom90:** [XeUnshackle](https://github.com/Byrom90/XeUnshackle)
- **Swizzy:** [Simple 360 NAND Flasher](https://github.com/Swizzy/XDK_Projects)
- **shutterbug2000** [ABadAvatar](https://github.com/shutterbug2000/ABadAvatar)
- **Team XeDEV:** XeXMenu
