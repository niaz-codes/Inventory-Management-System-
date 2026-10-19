# Installation Guide — NHI Inventory Management System

This guide covers installing the application on Windows using the provided installer.

## System requirements

| Item | Requirement |
| --- | --- |
| Operating system | Windows 10 or Windows 11 (64-bit) |
| Disk space | Approximately 400 MB free |
| Additional software | **None** — the required .NET runtime is bundled with the installer |
| Internet connection | Not required for normal offline use |

## How to install

1. Locate the **`NHI-Inventory-Setup.exe`** file.
2. Double-click the installer to start.
3. If Windows SmartScreen displays a warning, verify that you obtained the installer from a
   trusted source before deciding whether to continue.
4. Approve the Windows User Account Control (UAC) prompt if required.
5. Follow the installation wizard and choose your preferred installation folder, or keep the
   default:
   ```
   C:\Program Files\NHI Inventory Management System
   ```
6. Keep **Create a desktop shortcut** selected if you want easy access.
7. Click **Install** and wait for the installation to finish.
8. Click **Finish**. You can launch the application immediately if the option is available.

Once installed, open the application from the desktop shortcut or the Windows Start menu.

## Login information

| Field | Login details |
| --- | --- |
| Administrator username | `admin` |
| Administrator password | `REDACTED` |
| Shop-user username | `user` |
| Shop-user password | `user` |

> **Important:** change the administrator password after logging in. For security, avoid
> sharing your login credentials with unauthorised users.

## Data storage & backup

Business records are stored locally on your computer — no internet connection is needed.

**Where is my data stored?**

```
C:\Users\<YourWindowsUsername>\AppData\Local\NHiInventory\
```

This folder may contain:

- `inventory.db` — the database (shops, products, sales, customer khata, and more)
- `*.txt` — application settings such as language and scanning preferences
- `crash.log` — diagnostics written only if an unexpected error occurs

**How to back up your data**

1. Open NHI Inventory Management System.
2. Navigate to **Shop Management**.
3. Open **Backup & Restore**.
4. Follow the on-screen instructions to create a backup.

Keep an additional copy on a USB drive or another secure location.

## Moving to another computer

1. Install NHI Inventory Management System on the new computer.
2. Close the application on both computers.
3. Back up your existing data using the application's backup feature.
4. Transfer the backup to the new computer.
5. Use **Backup & Restore** to restore your records.

> Avoid copying a live database while the application is running, as this may result in an
> inconsistent backup.

## Updating the application

1. Download the new `NHI-Inventory-Setup.exe` from a trusted source.
2. Close the currently running application.
3. Run the new installer and follow the wizard.

The installer upgrades the existing installation while preserving your database and settings
(same application ID = in-place upgrade). Always create a backup before updating.

## Uninstalling the application

1. Open **Settings** in Windows.
2. Go to **Apps → Installed apps**.
3. Find **NHI Inventory Management System**.
4. Select **Uninstall** and follow the instructions.

Uninstalling is designed to **leave your local data folder intact**. If you want to
permanently remove your records, delete the `%LOCALAPPDATA%\NHiInventory\` folder manually
— but only after confirming you no longer need it.

## Troubleshooting

**The application does not open**

- Try launching it again from the desktop shortcut.
- Restart your computer and try again.
- Check whether Windows security software has blocked the application.

**The installer shows a security warning**

- Make sure the installer came from a trusted source. The installer is not code-signed, so
  Windows may display a warning; this alone does not prove the file is safe or unsafe.

**My data is missing**

- Check the local data folder (`%LOCALAPPDATA%\NHiInventory\`).
- Confirm you are using the correct Windows account.
- Restore your latest backup if necessary.

**Installing on another PC**

- Run the installer on the new PC, then restore your data from a backup.
