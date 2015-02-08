# EV3Basic
A basic compiler to target the Lego Mindstorms EV3 intelligent brick.

This programming language and development environment is intended as an easy way to use text based programming for the EV3. It was specifically designed to get things running quickly and should help novice programmers to get their first experience.


## Microsoft Small Basic Extension

One convenient way to develop programs for the EV3 is to use Microsoft Small Basic (download is free of charge) together with the EV3 extension. With this you have an easy to use development environment where you can build and run programs that control the EV3 brick from your PC. 

### Installation

1. Download and install Microsoft Small Basic from http://smallbasic.com/ (requires Microsoft Windows)
2. Get the ev3extension.zip from the current release.
3. Find the path where Small Basic was installed and extract the file ev3extension.zip to it (this should create a lib/ folder)

### Using Small Basic

1. Start Small Basic and begin writing your program. Use the intellisense documentation to learn about the various parts of the EV3 extension.
2. If you are a novice to programming, use the link "Beginning Small Basic" on the Small Basic homepage to learn about fundamental concepts and how to generally create programs.
3. Run your program directly from the Small Basic environment. When you access functions for the EV3 brick (Everything in EV3, LCD, Motor, Sensor, Buttons, Speaker), the program tries to access and control the EV3 brick via an USB connection.
 
 
## EV3 Explorer (with compiler)

The EV3 Explorer can be used to view and organize the files that are currently stored on the EV3 brick. But most importantly it has a built-in compiler that can convert your Small Basic programs to a form that can be executed directly on the brick.

### Installation and Startup

Using the program requires at least .NET 3.5 which is the same as is required to run Small Basic.

1. Download ev3explorer.zip from the current release
2. Extract to a folder of your choice
3. Start the file EV3Explorer.exe

### Use EV3Explorer

The main window is divided in two parts. The left one shows the file system of the EV3, the right shows your local PC file system (both with navigation buttons on top).
To transfer (and optionally compile) files, select one file in the right window and click one of the "Compile" or "Download" buttons.
Most programs will compile to the EV3 without much problems as long as you do not use unsupported functions. But there are subtle differences for some language constructs which will cause compilation to fail with an error.
