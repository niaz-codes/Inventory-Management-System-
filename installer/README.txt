================================================================
 NHI Inventory Management System - Installer
================================================================

WHAT'S IN THIS FOLDER
  NHI-Inventory-Setup.exe   The complete installer. This one file is
                            everything - share it by USB, email or download.

--------------------------------------------------------------
INSTALL (on any Windows 10/11 64-bit PC)
--------------------------------------------------------------
  1. Double-click  NHI-Inventory-Setup.exe
  2. If Windows SmartScreen shows a blue box:
        click "More info"  ->  "Run anyway"
     (this appears only because the file is not code-signed;
      it is safe.)
  3. Approve the User Account Control (UAC) prompt.
  4. Follow the wizard.  You can choose the install folder;
     the default is:
        C:\Program Files\NHI Inventory Management System
  5. Leave "Create a desktop shortcut" ticked and click Install.
  6. Finish.  Tick "Launch NHI Inventory Management System" to
     open it right away.

After install you get:
  * A Desktop shortcut  ->  "NHI Inventory Management System"
  * Start Menu entries:
       - NHI Inventory Management System        (opens the app)
       - Uninstall NHI Inventory Management System
  * An entry in  Settings > Apps > Installed apps
    (and Control Panel > Programs and Features)

First login:
  Username:  admin
  Password:  admin
  (change it from Settings after logging in)

--------------------------------------------------------------
WHERE YOUR DATA LIVES
--------------------------------------------------------------
  C:\Users\<you>\AppData\Local\NHiInventory\
      inventory.db     - all shops, products, sales, khata, etc.
      *.txt            - app settings (language, scan mode, ...)

  This folder is OUTSIDE the install folder.  Updating or
  uninstalling the app NEVER deletes it.  To move the app to a
  new PC, copy this folder across after installing.

  Back it up from inside the app:
      Shop Management  ->  "Backup & Restore"

--------------------------------------------------------------
UPDATING TO A NEWER VERSION
--------------------------------------------------------------
  Just run the newer NHI-Inventory-Setup.exe.  It detects the
  existing installation and upgrades it in place.  Your database
  and settings are kept.  No need to uninstall first.

--------------------------------------------------------------
UNINSTALL
--------------------------------------------------------------
  Settings > Apps > "NHI Inventory Management System" > Uninstall
  ...or the Start Menu "Uninstall" shortcut.

  This removes the program only.  Your data folder
  (AppData\Local\NHiInventory) is left in place on purpose so
  you don't lose records.  Delete it by hand if you truly want
  a clean slate.

--------------------------------------------------------------
REQUIREMENTS
--------------------------------------------------------------
  * Windows 10 or 11, 64-bit
  * ~400 MB free disk space
  * No separate .NET install needed - the runtime is bundled.
