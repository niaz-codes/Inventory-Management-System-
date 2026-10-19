                                                             NHI Inventory Management System
A simple, offline inventory management application designed to help shop owners manage their daily business activities, including products, sales, stock, and customer records.
Installation Guide
The application comes with a Windows installer, so you don't need to install additional software or configure anything manually.
System Requirements
•	Operating System: Windows 10 or Windows 11 (64-bit)
•	Disk Space: Approximately 400 MB of free space
•	Additional Software: Not required. The necessary .NET runtime is included with the application.
•	Internet Connection: Not required for normal offline use.
How to Install
1.	Locate the NHI-Inventory-Setup.exe file.
2.	Double-click the installer to start the installation.
3.	If Windows SmartScreen displays a warning, verify that you obtained the installer from a trusted source before deciding whether to continue.
4.	Approve the Windows User Account Control (UAC) prompt if required.
5.	Follow the installation wizard and choose your preferred installation folder, or keep the default location:
C:\Program Files\NHI Inventory Management System
6.	Keep Create a desktop shortcut selected if you want easy access to the application.
7.	Click Install and wait for the installation to finish.
8.	Click Finish to complete the setup. You can launch the application immediately if the option is available.
Once installed, you can open the application from your desktop shortcut or the Windows Start Menu.
Login Information
Use the following credentials to access the application:
Field	Login Details
Username	user
Password	user
Important: Change the default password after logging in if the application supports password changes. For security, avoid sharing your login credentials with unauthorized users.
Data Storage and Backup
Your business records are stored locally on your computer. The application is designed for offline use, so an internet connection is not needed for normal operations.
Where Is My Data Stored?
The database and application settings are saved in the following location:
C:\Users\<YourWindowsUsername>\AppData\Local\NHiInventory\
This folder may contain:
•	inventory.db — your inventory database, including shops, products, sales, customer khata records, and other stored business information.
•	*.txt — application settings, such as language and scanning preferences.
The exact records available depend on the features you use in the application.
How to Back Up Your Data
To protect your business records:
1.	Open NHI Inventory Management System.
2.	Navigate to Shop Management.
3.	Open Backup & Restore.
4.	Follow the available backup instructions.
Keep an additional backup on a USB drive or another secure storage location.
Moving to Another Computer
To transfer your data to a new Windows PC:
1.	Install NHI Inventory Management System on the new computer.
2.	Close the application on both computers.
3.	Back up your existing data using the application's backup feature.
4.	Transfer the backup to the new computer.
5.	Use the application's Backup & Restore feature to restore your records.
Avoid copying a live database while the application is running, as this may result in an inconsistent backup.
Updating the Application
When a newer version becomes available:
1.	Download the new NHI-Inventory-Setup.exe from a trusted source.
2.	Close the currently running application.
3.	Run the new installer.
4.	Follow the installation wizard to complete the update.
The installer is intended to upgrade the existing installation while preserving your database and settings. However, always create a backup before updating, just in case something goes wrong.
Uninstalling the Application
If you no longer need the application:
1.	Open Settings in Windows.
2.	Go to Apps → Installed apps.
3.	Find NHI Inventory Management System.
4.	Select Uninstall and follow the instructions.
You can also use the uninstall shortcut in the Windows Start Menu, if available.
Your data: Uninstalling the application is designed to leave your local data folder intact. This helps protect your business records, but you should still create a backup before uninstalling. If you want to permanently remove your records, delete the remaining data folder manually only after confirming that you no longer need it.
Troubleshooting
The application does not open
•	Try launching it again from the desktop shortcut.
•	Restart your computer and try again.
•	Check whether Windows security software has blocked the application.
The installer shows a security warning
•	Make sure the installer came from a trusted source.
•	Because an installer is not code-signed, Windows may display a warning. This warning alone does not prove that the file is safe or unsafe.
My data is missing
•	Check the local data folder.
•	Confirm that you are using the correct Windows account.
•	Restore your latest backup if necessary.
I want to install the application on another PC
•	Run the installer on the new PC and restore your data from a backup.
About the Project
NHI Inventory Management System is an offline desktop application developed to make everyday inventory and shop management easier.
The goal is to provide a straightforward way to manage business records without depending on an internet connection.
License and Distribution
This README describes how to install and use the application. Refer to the project's actual license or distribution terms for information about copying, modifying, or redistributing the software.
________________________________________
NHI Inventory Management System
Simple. Practical. Designed for offline business management.

