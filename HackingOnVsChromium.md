Walkthrough for your first time hacking on vs-chromium.

It's pretty easy actually, but newbie guides like this (which folks rarely
consulted more than once) tends to fall out of date. So if you're following this
guide and hit trouble, please try to fix what you see -- as you (the newbie) are
a better position than anyone else to do so.

### Prereqs
Obtain a Community, Professional or better edition of Visual Studio 2013. The
Express edition does not support vs-chromium.

Download and install the Visual Studio 2013 SDK

This SDK enables development of IDE extensions like vs-chromium.

http://www.microsoft.com/en-us/download/details.aspx?id=40758

### Get the code
    cd D:\src\ (or some such)
    git clone https://github.com/chromium/vs-chromium.git

### Building and running the code.
Open `vs-chromium\src\vs-chromium.sln`

Maybe start by building everything (Debug config) via `F7`. If you get errors,
try figuring out the problem and don't forget to update this guide with the
missing prereq.

Find the VsChromium project. This builds to the .vsix binary that is the
extension installer.

Find the Tests project. This builds to a VS unit test (not gtest) dll.

These tests can be run via  the top-level Test menu in the IDE:

`[Test -> Run -> All Tests (Ctrl+R, A)]`

The results show up in the Test Explorer window:

`[Test -> Windows -> Test Explorer]`

Clicking around (and right clicking) lets you interact with failures.

To debug the extension, set VsChromium as your start-up project and start a
debugging session via F5. This will spin up an experimental instance of
Visual Studio running the extension. The first launch is markedly slow as
the profile or something is created, but subsequent launches are faster.
You'll be able to debug the code that runs inside the visual studio project,
but vs-chromium also spins up a server process to do file system indexing,
and you may want to debug inside that process too. To do so:

  1. Start by setting a breakpoint in
     `ServerProxyProcess.cs!AfterProxyCreated`. This should hit (in your
     devenv.exe process) shortly after the server process is launched.

  2. When the breakpoint is hit, do `Ctrl+Alt+P` to open the "attach to
     process" dialog. The server process is named `VsChromium.Server.exe`.
     Attach to it.

  3. Now your breakpoints in the child process will work too.

### Profiling or Debugging the server process
The easiest option for debugging is to simply run the VsChromium project
as usual, then Attach the VS Debugger to the `VsChromium.Server.exe` process

To Debug startup code (or to run the Server from an external profile such as
DotMemory), follow these steps:
1. Setup the solution as in the previous section
2. Rename the "PROFILE_SERVER2" to "PROFILE_SERVER" "Conditional compilation symbol"
   in the "Properties" of the VsChromium project 
3. Build
4. Run Visual Studio with experimental root suffix from a command prompt
   (it seems that it is required to use an "Admin" command prompt)

   Ex: `"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" /rootSuffix Exp`
4. Run the "Server" project (ensure the command line argument is set to `63300`
   which is the default TCP port to use when debugging
