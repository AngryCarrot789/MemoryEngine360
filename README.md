# MemEngine360 v1.1
This is a remake of Cheat Engine, but for consoles. Currently, only the Xbox360 is supported. Still WIP

![](MemEngine360.Avalonia_2025-04-27_01.17.35.png)

This project was inspired from https://github.com/XeClutch/Cheat-Engine-For-Xbox-360

# Download and built
Clone repo: `git clone --recursive https://github.com/AngryCarrot789/MemEngine360`
Open `MemEngine360.sln` and build/run 

# How to use
Your console needs to be running xmdb. Press `CTRL + O` or go to `File>Connect to console`. Specify the console's IP address and click `Connect`

![](MemEngine360.Avalonia_2025-04-23_00.40.36.png)

A dialog will popup with some information about your console, such as debug name and all threads running

You'll see in the bottom right corner a progress bar will sometimes appear. They represent 'Activities', 
such as read/write operation status, scan status, and more.

![](rider64_2025-04-27_22.07.31.png)

## Scanning
Enter a value in the `Value` field, select the data type you wish to scan for (e.g. Byte, Int32, String), then below that, you can 
specify search options such as the `Scan Type` (match equal values, less than, between and more), and string type (ASCII, UTF32, etc.)

Then, specify a start address in the `Memory Scanning Options` panel and also how many bytes you want to read (default is `0x10000000` or 256MB).

You can also click the little green memory button to open a dialog, which lets you select a memory region to scan

![](MemEngine360-DesktopUI_2025-04-27_22.02.37.png)
~~~~
If you wish, you can enable DEBUG PAUSE which will freeze the xbox during scan (speeds up scan and useful if you don't want values to change during scan). 
If you wish, you can disable Scan Memory Pages, though there isn't much point of doing so.

Then, click `First Scan`. The activity status (bottom right) shows the scan progress. You can cancel the scan by clicking the X.

If Scan Memory Pages is disabled, the status will show 2 messages:
- `Reading chunk x/y` - We read data from the console in chunks of 65536 bytes. So if you scan for 200,000 bytes, it requires 4 chunks to be read
- `Scanning chunk x/y` - The program is scanning for the value in the chunk. This is typically extremely fast compared to Reading Chunk so it will only flash for a split second

Otherwise it shows only one message:
- `Region a/b (c/d)` -- It's processing region A out of B, and has read C out of D bytes from the console 

Then once the scan is complete, it may show `Updating result list...`. This is where it adds the results into the UI at a steady pace to prevent the UI freezing (rate of about 2000/s (system performance dependent))


Then, if you want to check if any results' current value have changed, click `Next Scan` and it will read the current value of all results
and compare it to the value field(s) and remove any results that no longer match (because the value changed)

## Saved addresses
If you wish to keep an eye on specific addresses, you can add entries in here. 

You can select results in the scan results panel, then click `Add Scan Result(s)` in the Saved Addresses panel to automatically add them. 

Or, you can add them manually by clicking `Add Entry`. Then, double click cell in the `Data Type` column (it says `Byte` by default), which
shows a popup to modify the data type. You can specify the length of a string in here too if you specify the data type as string.

## Changing values
You can double click the cell in the `Value` column(s) to modify that cell. 
You can also select multiple rows and click `CTRL + E` to modify the value of all of them.

![](MemEngine360.Avalonia_2025-04-23_01.04.01.png)

## Copying scan results
Select any number of scan results and press `CTRL + C`. A dialog will show the results formatted in CSV

## Deleting rows
Select any scan result or saved address rows and press the Delete key to remove them.

### Remote Controls
There's a few remote control commands you can find in the `Remote Controls` menu. These include:
- `Open Disk Tray` - Opens the console's disk tray (cannot be closed remotely)
- `Debug Freeze` - Freezes the console
- `Debug Unfreeze` - Unfreezes the console
- `Soft Reboot` - Reboots the current title
- `Cold Reboot` - Fully reboots the console (shows the xbox boot animation)
- `Shutdown` - Tells the console to shutdown
