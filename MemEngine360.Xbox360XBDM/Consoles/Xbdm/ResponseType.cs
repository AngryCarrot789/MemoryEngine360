// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public enum ResponseType : int {
    SingleResponse = 200,
    Connected = 201, // XBDM_CONNECTED
    MultiResponse = 202, // XBDM_MULTIRESPONSE // terminates with period
    BinaryResponse = 203, // XBDM_BINRESPONSE
    ReadyForBinary = 204, // XBDM_READYFORBIN

    /// <summary>A connection has been dedicated to a specific threaded command handler.</summary>
    DedicatedConnection = 205, // XBDM_DEDICATED // notificaiton channel/ dedicated connection (XBDM_DEDICATED)

    /// <summary>No error occurred.</summary>
    NoError = 400, // XBDM_NOERR

    /// <summary>An undefined error has occurred.</summary>
    UndefinedError = 400, // XBDM_UNDEFINED

    /// <summary>The maximum number of connections has been exceeded.</summary>
    MaxConnectionsExceeded = 401, // XBDM_MAXCONNECT

    /// <summary>No such file exists.</summary>
    FileNotFound = 402, // XBDM_NOSUCHFILE

    /// <summary>No such module exists.</summary>
    NoSuchModule = 403, // XBDM_NOMODULE

    /// <summary>The referenced memory has been unmapped. setzerobytes or setmem failed</summary>
    MemoryNotMapped = 404, // XBDM_MEMUNMAPPED    

    /// <summary>No such thread ID exists.</summary>
    NoSuchThread = 405, // XBDM_NOTHREAD

    /// <summary>The console clock is not set. linetoolong or clocknotset</summary>
    ClockNotSet = 406, // XBDM_CLOCKNOTSET   

    /// <summary>An invalid command was specified.</summary>
    UnknownCommand = 407, // XBDM_INVALIDCMD

    /// <summary>Thread not stopped.</summary>
    NotStopped = 408, // XBDM_NOTSTOPPED

    /// <summary>File must be copied, not moved.</summary>
    FileMustBeCopied = 409, // XBDM_MUSTCOPY

    /// <summary>A file already exists with the same name.</summary>
    FileAlreadyExists = 410, // XBDM_ALREADYEXISTS

    /// <summary>The directory is not empty.</summary>
    DirectoryNotEmpty = 411, // XBDM_DIRNOTEMPTY

    /// <summary>An invalid file name was specified.</summary>
    BadFileName = 412, // XBDM_BADFILENAME

    /// <summary>Cannot create the specified file.</summary>
    FileCannotBeCreated = 413, // XBDM_CANNOTCREATE

    /// <summary>Cannot access the specified file.</summary>
    AccessDenied = 414, // XBDM_CANNOTACCESS

    /// <summary>The device is full.</summary>
    DeviceIsFull = 415, // XBDM_DEVICEFULL

    /// <summary>This title is not debuggable.</summary>
    NotDebuggable = 416, // XBDM_NOTDEBUGGABLE

    /// <summary>The counter type is invalid.</summary>
    CountTypeInvalid = 417, // XBDM_BADCOUNTTYPE

    /// <summary>Counter data is not available.</summary>
    CountNotAvailable = 418, // XBDM_COUNTUNAVAILABLE

    /// <summary>The console is not locked.</summary>
    BoxIsNotLocked = 420, // XBDM_NOTLOCKED

    /// <summary>Key exchange is required.</summary>
    KeyExchangeRequired = 421, // XBDM_KEYXCHG

    /// <summary>A dedicated connection is required.</summary>
    DedicatedConnectionRequired = 422, // XBDM_MUSTBEDEDICATED

    /// <summary>The argument was invalid.</summary>
    InvalidArgument = 423, // XBDM_INVALIDARG

    /// <summary>The profile is not started.</summary>
    ProfileNotStarted = 424, // XBDM_PROFILENOTSTARTED

    /// <summary>The profile is already started.</summary>
    ProfileAlreadyStarted = 425, // XBDM_PROFILEALREADYSTARTED

    /// <summary>The console is already in DMN_EXEC_STOP.</summary>
    XBDM_ALREADYSTOPPED = 400 + 0x1A,

    /// <summary>FastCAP is not enabled.</summary>
    XBDM_FASTCAPNOTENABLED = 400 + 0x1B,

    /// <summary>The Debug Monitor could not allocate memory.</summary>
    XBDM_NOMEMORY = 400 + 0x1C,

    /// <summary>Initialization through DmStartProfiling has taken longer than allowed.</summary>
    XBDM_TIMEOUT = 400 + 0x1D,

    /// <summary>The path was not found.</summary>
    XBDM_NOSUCHPATH = 400 + 0x1E,

    /// <summary>The screen input format is invalid.</summary>
    XBDM_INVALID_SCREEN_INPUT_FORMAT = 400 + 0x1F,

    /// <summary>The screen output format is invalid.</summary>
    XBDM_INVALID_SCREEN_OUTPUT_FORMAT = 400 + 0x20,

    /// <summary>CallCAP is not enabled.</summary>
    XBDM_CALLCAPNOTENABLED = 400 + 0x21,

    /// <summary>Both FastCAP and CallCAP are enabled in different modules.</summary>
    XBDM_INVALIDCAPCFG = 400 + 0x22,

    /// <summary>Neither FastCAP nor CallCAP are enabled.</summary>
    XBDM_CAPNOTENABLED = 400 + 0x23,

    /// <summary>A branched to a section the instrumentation code failed.</summary>
    XBDM_TOOBIGJUMP = 400 + 0x24,

    /// <summary>A necessary field is not present in the header of Xbox 360 title.</summary>
    XexFieldNotFound = 400 + 0x25, // XBDM_FIELDNOTPRESENT  

    /// <summary>Provided data buffer for profiling is too small.</summary>
    XBDM_OUTPUTBUFFERTOOSMALL = 400 + 0x26,

    /// <summary>The Xbox 360 console is currently rebooting.</summary>
    XBDM_PROFILEREBOOT = 400 + 0x27,

    /// <summary>The maximum duration was exceeded.</summary>
    XBDM_MAXDURATIONEXCEEDED = 400 + 0x29,

    /// <summary>The current state of game controller automation is incompatible with the requested action.</summary>
    XBDM_INVALIDSTATE = 400 + 0x2A,

    /// <summary>The maximum number of extensions are already used.</summary>
    XBDM_MAXEXTENSIONS = 400 + 0x2B,

    /// <summary>The Performance Monitor Counters (PMC) session is already active.</summary>
    XBDM_PMCSESSIONALREADYACTIVE = 400 + 0x2C,

    /// <summary>The Performance Monitor Counters (PMC) session is not active.</summary>
    XBDM_PMCSESSIONNOTACTIVE = 400 + 0x2D,

    /// <summary> </summary>
    XBDM_LINE_TOO_LONG = 400 + 0x2E,

    /// <summary>The current application has an incompatible version of D3D.</summary>
    D3DDebugCommandNotImplemented = 480, // XBDM_D3D_DEBUG_COMMAND_NOT_IMPLEMENTED

    /// <summary>The D3D surface is not currently valid.</summary>
    D3DInvalidSurface = 481, // XBDM_D3D_INVALID_SURFACE
    VxTaskPending = 496,
    VxTooManySessions = 497

    /*
        SingleResponse = 200,
    Connected = 201,
    MultiResponse = 202, //terminates with period
    BinaryResponse = 203,
    ReadyForBinary = 204,
    NowNotifySession = 205, // notificaiton channel/ dedicated connection (XBDM_DEDICATED)
    UndefinedError = 400,
    MaxConnectionsExceeded = 401,
    FileNotFound = 402,
    NoSuchModule = 403,
    MemoryNotMapped = 404, //setzerobytes or setmem failed
    NoSuchThread = 405,
    ClockNotSet = 406, //linetoolong or clocknotset
    UnknownCommand = 407,
    NotStopped = 408,
    FileMustBeCopied = 409,
    FileAlreadyExists = 410,
    DirectoryNotEmpty = 411,
    BadFileName = 412,
    FileCannotBeCreated = 413,
    AccessDenied = 414,
    NoRoomOnDevice = 415,
    NotDebuggable = 416,
    TypeInvalid = 417,
    DataNotAvailable = 418,
    BoxIsNotLocked = 420,
    KeyExchangeRequired = 421,
    DedicatedConnectionRequired = 422,
    InvalidArgument = 423,
    ProfileNotStarted = 424,
    ProfileAlreadyStarted = 425,
    D3DDebugCommandNotImplemented = 480,
    D3DInvalidSurface = 481,
     */
}