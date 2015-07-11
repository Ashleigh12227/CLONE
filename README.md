xRAT 2.0
========

[![Join the chat at https://gitter.im/MaxXor/xRAT](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/MaxXor/xRAT?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/na7hitbqx8327xr9?svg=true)](https://ci.appveyor.com/project/MaxXor/xrat)

**Free, Open-Source Remote Administration Tool**

xRAT 2.0 is a fast and light-weight remote administration tool coded in C#. Providing high stability and an easy-to-use user interface, xRAT is the perfect remote administration solution for you.

Features
---
* Buffered TCP/IP network stream
* Protocol Buffers to send & receive serialized data
* Compressed (QuickLZ) & Encrypted (AES-128) communication
* Multi-Threaded
* UPnP Support
* No-Ip.com Support
* Custom social engineering tactic to elevate Admin privileges (betabot's trick)
* Visit Website (hidden & visible)
* Show Messagebox
* Task Manager
* File Manager
* Startup Manager
* Remote Desktop
* Remote Shell
* Download & Execute
* Upload & Execute
* System Information
* Computer Commands (Restart, Shutdown, Standby)
* Keylogger
* Reverse SOCKS5/HTTPS Proxy

Requirements
---
* .NET Framework 3.5 Client Profile ([Download](https://www.microsoft.com/en-US/download/details.aspx?id=14037))
* Supported Operating Systems (32- and 64-bit)
  * Windows XP
  * Windows Server 2003
  * Windows Vista
  * Windows Server 2008
  * Windows 7
  * Windows Server 2012
  * Windows 8/8.1
  * Windows 10 Preview

Compiling
---
Open the project in Visual Studio and click build, or use one of the batch files included in the root directory.

| Batch file        | Description
| ----------------- |:-------------
| build-debug.bat   | Builds the application using the debug configuration (for testing)
| build-release.bat | Builds the application using the release configuration  (for publishing)

Building a client
---
| Build configuration         | Description
| ----------------------------|:-------------
| debug configuration         | The pre-defined [Settings.cs](/Client/Config/Settings.cs) will be used. The client builder does not work in this configuration. You can execute the client directly with the specified settings.
| release configuration       | Use the client builder to build your client otherwise it is going to crash.

ToDo
---
* Registry Editor (browse, delete, add registry keys)
* Password Recovery
 * Recover Passwords of common browsers (i.e. Chrome, Firefox, IE) and FTP-Clients (i.e. FileZilla Client)
* Startup Persistence
* [Issues](https://github.com/MaxXor/xRAT/issues)

Contributing
---
See [CONTRIBUTING.md](/CONTRIBUTING.md)

License
---
See [LICENSE.md](/LICENSE.md)

Donate
---
BTC: `1EWgMfBw1fUSWMfat9oY8t8qRjCRiMEbET`

Credits
---
Protocol Buffers - Google's data interchange format  
Copyright (c) 2008 Google Inc.  
https://developers.google.com/protocol-buffers/

ResourceLib  
Copyright (c) Daniel Doubrovkine, Vestris Inc., 2008-2013  
https://github.com/dblock/resourcelib

GlobalMouseKeyHook  
Copyright (c) 2004-2015, George Mamaladze  
https://github.com/gmamaladze/globalmousekeyhook

Cecil  
Copyright (c) 2008 - 2015 Jb Evain Copyright (c) 2008 - 2011 Novell, Inc.  
https://github.com/jbevain/cecil

Thank you!
---
I really appreciate all kinds of feedback and contributions. Thanks for using and supporting xRAT 2.0!
